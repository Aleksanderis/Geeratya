using System.Net;
using System.Text.Json;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

/// <summary>The sprint writes through the write handler, against the recorded sprint replies.</summary>
[Trait("Os", "Independent")]
public sealed class WriteHandlerSprintTests
{
    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _end = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GivenACreateRequest_WhenExecuting_ThenOnePostCarriesTheBoardNameGoalAndNoIssueIsRead()
    {
        using var fixture = await ConnectedAsync(
            handler => handler.Reply(HttpStatusCode.Created, ScrumFixture("write-sprint-create.json")));
        var request = new CreateSprintRequest(6, "s172 tmp 6", "survey goal", _start, _end);

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(41, request));

        Assert.Empty(Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted)).Rows);
        Assert.Equal(["POST"], fixture.Http.Methods);
        Assert.EndsWith("/rest/agile/1.0/sprint", Assert.Single(fixture.Http.Requests).AbsolutePath);
        using var body = JsonDocument.Parse(Assert.Single(fixture.Http.Bodies));
        Assert.Equal(6, body.RootElement.GetProperty("originBoardId").GetInt64());
        Assert.Equal("s172 tmp 6", body.RootElement.GetProperty("name").GetString());
        Assert.Equal("survey goal", body.RootElement.GetProperty("goal").GetString());
        Assert.Equal("2026-11-02T10:00:00.000Z", body.RootElement.GetProperty("startDate").GetString());
    }

    [Fact]
    public async Task GivenAnEditRequest_WhenExecuting_ThenOnlyTheChangedMembersGoToThatSprint()
    {
        using var fixture = await ConnectedAsync(
            handler => handler.Reply(HttpStatusCode.OK, ScrumFixture("write-sprint-partial-update.json")));
        var request = new EditSprintRequest(6, Before(), new SprintEdit("renamed", null, null, null));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(41, request));

        Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.EndsWith("/rest/agile/1.0/sprint/12", Assert.Single(fixture.Http.Requests).AbsolutePath);
        using var body = JsonDocument.Parse(Assert.Single(fixture.Http.Bodies));
        Assert.Equal(["name"], body.RootElement.EnumerateObject().Select(member => member.Name));
    }

    [Fact]
    public async Task GivenAnEndBeforeTheStart_WhenExecuting_ThenNothingIsSentAndTheEndFieldCarriesTheMessage()
    {
        using var fixture = await ConnectedAsync(_ => { });
        var edit = new SprintEdit(null, null, _end, _start);

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(41, new EditSprintRequest(6, Before(), edit)));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Empty(fixture.Http.Requests);
        Assert.Equal("endDate", Assert.Single(failed.Error.FieldErrors).FieldId);
    }

    [Fact]
    public async Task GivenJiraRefusesWithAFieldError_WhenExecuting_ThenItFailsDefinitelyAndIsSentOnce()
    {
        const string reply = """{"errorMessages":[],"errors":{"name":"Name too long"}}""";
        using var fixture = await ConnectedAsync(handler => handler.Reply(HttpStatusCode.BadRequest, reply));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(41, new CreateSprintRequest(6, "x", null, null, null)));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal("Name too long", Assert.Single(failed.Error.FieldErrors).Message);
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenALostReply_WhenCreating_ThenItIsNotRepeated()
    {
        using var fixture = await ConnectedAsync(
            handler => handler.Reply(HttpStatusCode.BadGateway, string.Empty).Reply(HttpStatusCode.Created, "{}"),
            3);

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(41, new CreateSprintRequest(6, "x", null, null, null)));

        Assert.IsType<WriteOutcomeUnknown>(Assert.Single(fixture.Posted));
        Assert.Single(fixture.Http.Requests);
    }

    [Theory]
    [InlineData("s172 tmp 6 renamed", VerifyOutcome.Applied)]
    [InlineData("something else", VerifyOutcome.NotApplied)]
    public async Task GivenALostEditReply_WhenVerifying_ThenTheSprintReadDecidesTheOutcome(
        string name,
        VerifyOutcome outcome)
    {
        using var fixture = await ConnectedAsync(
            handler => handler.Reply(HttpStatusCode.OK, ScrumFixture("write-sprint-partial-update.json")));
        var request = new EditSprintRequest(6, Before(), new SprintEdit(name, null, null, null));

        await fixture.Writes.StartAsync(new VerifyWriteEffect(41, request));

        Assert.Equal(outcome, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
        Assert.EndsWith("/rest/agile/1.0/sprint/12", Assert.Single(fixture.Http.Requests).AbsolutePath);
    }

    [Fact]
    public async Task GivenALostCreateReply_WhenVerifying_ThenNothingIsReadAndTheOutcomeIsUnknown()
    {
        using var fixture = await ConnectedAsync(_ => { });

        await fixture.Writes.StartAsync(
            new VerifyWriteEffect(41, new CreateSprintRequest(6, "x", null, null, null)));

        Assert.Equal(VerifyOutcome.Unreachable, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
        Assert.Empty(fixture.Http.Requests);
    }

    private static Sprint Before()
    {
        return new Sprint(12, "s172 tmp 6", SprintState.Future, null, null, null, null, 6);
    }

    private static string ScrumFixture(string name)
    {
        return RecordedFixtures.Read($"scrum/{name}");
    }

    private static async Task<Fixture> ConnectedAsync(Action<ScriptedJiraHandler> script, int maxRetries = 0)
    {
        var http = new ScriptedJiraHandler();
        var store = new FakeSecretStore();

        script(http);
        await store.SetAsync(_a.CredentialKey, "token-a", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = maxRetries,
            Delay = (_, _) => Task.CompletedTask,
            Jitter = () => 0.5,
        };

        var connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            new FakeTransportFactory(http, options));

        await connections.ActivateAsync(_a, 2, CancellationToken.None);

        return new Fixture(connections, http);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(SiteConnections connections, ScriptedJiraHandler http)
        {
            Connections = connections;
            Http = http;
            Runner = new NetworkRunner(connections, new ListCommandSink(Posted), CancellationToken.None);
            Writes = new WriteHandler(Runner);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public WriteHandler Writes { get; }

        public List<Command> Posted { get; } = [];

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }
    }
}
