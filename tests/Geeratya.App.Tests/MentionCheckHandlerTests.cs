using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Comments;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public sealed class MentionCheckHandlerTests
{
    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly MentionedPerson _olga = new("acc-olga", "Olga Other");
    private static readonly MentionedPerson _ann = new("acc-ann", "Ann");

    [Fact]
    public async Task GivenOnePersonCanSeeTheIssueAndOneCannot_WhenChecking_ThenOnlyTheOtherIsReported()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.OK, """[{"accountId":"acc-olga","displayName":"Olga Other","active":true}]""")
            .Reply(HttpStatusCode.OK, """[{"accountId":"acc-someone","displayName":"Ann Else","active":true}]"""));

        await fixture.Handler.StartAsync(Effect(_olga, _ann));

        var reply = Assert.IsType<MentionsChecked>(Assert.Single(fixture.Posted));
        Assert.Equal([_ann], reply.Unseen);
        Assert.Equal("5", reply.CommentId);
        Assert.Equal(2, fixture.Http.Requests.Count);
        Assert.All(fixture.Http.Methods, method => Assert.Equal("GET", method));
        Assert.All(fixture.Http.Requests, uri => Assert.EndsWith("/rest/api/3/user/viewissue/search", uri.AbsolutePath));
        Assert.Contains("issueKey=A-1", fixture.Http.Requests.First().Query);
        Assert.Contains("query=Olga", fixture.Http.Requests.First().Query);
    }

    [Fact]
    public async Task GivenEveryoneCanSeeTheIssue_WhenChecking_ThenAnEmptyAnswerIsPosted()
    {
        using var fixture = await ConnectedAsync(
            h => h.Reply(HttpStatusCode.OK, """[{"accountId":"acc-olga","displayName":"Olga Other","active":true}]"""));

        await fixture.Handler.StartAsync(Effect(_olga));

        Assert.Empty(Assert.IsType<MentionsChecked>(Assert.Single(fixture.Posted)).Unseen);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GivenTheLookupFails_WhenChecking_ThenNobodyIsFlaggedAndNothingIsPosted(HttpStatusCode status)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(status, string.Empty));

        await fixture.Handler.StartAsync(Effect(_olga));

        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenAFullPageWithoutThePerson_WhenChecking_ThenTheAnswerProvesNothingAndTheyAreNotFlagged()
    {
        var users = string.Join(
            ',',
            Enumerable.Range(0, 20).Select(n => $$"""{"accountId":"acc-{{n}}","displayName":"Olga {{n}}","active":true}"""));
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, $"[{users}]"));

        await fixture.Handler.StartAsync(Effect(_olga));

        Assert.Empty(Assert.IsType<MentionsChecked>(Assert.Single(fixture.Posted)).Unseen);
    }

    private static CheckMentionsEffect Effect(params MentionedPerson[] people)
    {
        return new CheckMentionsEffect(new TabId(1), "A-1", "5", people);
    }

    private static async Task<Fixture> ConnectedAsync(Action<ScriptedJiraHandler> script)
    {
        var http = new ScriptedJiraHandler();
        var store = new FakeSecretStore();

        script(http);
        await store.SetAsync(_profile.CredentialKey, "token-a", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = 0,
            Delay = (_, _) => Task.CompletedTask,
            Jitter = () => 0.5,
        };
        var connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            new FakeTransportFactory(http, options));

        await connections.ActivateAsync(_profile, 1, CancellationToken.None);

        return new Fixture(connections, http);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(SiteConnections connections, ScriptedJiraHandler http)
        {
            Connections = connections;
            Http = http;
            Runner = new NetworkRunner(connections, new ListCommandSink(Posted), CancellationToken.None);
            Handler = new MentionCheckHandler(Runner);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public MentionCheckHandler Handler { get; }

        public List<Command> Posted { get; } = [];

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }
    }
}
