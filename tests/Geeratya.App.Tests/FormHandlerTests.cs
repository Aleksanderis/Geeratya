using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Forms;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes.Models;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public sealed class FormHandlerTests
{
    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly TabId _tab = new(1);

    [Fact]
    public async Task GivenSyntheticEditMetadata_WhenTheFormLoads_ThenEveryFieldIsListedAndTheUnknownTypeIsReadOnly()
    {
        var meta = RecordedFixtures.Read("metadata/synthetic-editmeta-unknown-types.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, meta));
        var target = new EditFormTarget("A-1", new Dictionary<string, FormValue>(), false, null);

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, target));

        var loaded = Assert.IsType<FormMetadataLoaded>(Assert.Single(fixture.Posted));
        var byId = loaded.Fields.ToDictionary(field => field.Id);
        Assert.Equal(7, loaded.RequestId);
        Assert.Equal(FormFieldKind.Text, byId["summary"].Kind);
        Assert.Equal(FormFieldKind.Paragraph, byId["customfield_90002"].Kind);
        Assert.Equal(FormFieldKind.Number, byId["customfield_90003"].Kind);
        Assert.Equal(FormFieldKind.DateTime, byId["customfield_90006"].Kind);
        Assert.Equal(FormFieldKind.Unsupported, byId["customfield_90001"].Kind);
        Assert.False(byId["customfield_90001"].IsEditable);
        Assert.False(byId["customfield_90005"].IsEditable);
        Assert.EndsWith("/rest/api/3/issue/A-1/editmeta", fixture.Http.Requests.Single().AbsolutePath);
    }

    [Fact]
    public async Task GivenAMissingIssue_WhenTheFormLoads_ThenTheFailureNamesTheIssue()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NotFound, "{}"));
        var target = new EditFormTarget("A-1", new Dictionary<string, FormValue>(), false, null);

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, target));

        var failed = Assert.IsType<FormMetadataFailed>(Assert.Single(fixture.Posted));
        Assert.Equal("A-1 not found or no access", failed.Message);
    }

    [Fact]
    public async Task GivenALabelsEdit_WhenExecuted_ThenOnePutCarriesOnlyTheAddsAndRemovesNotTheWholeSet()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, """{"issues":[],"isLast":true}"""));
        var change = new FieldChange(
            "labels",
            FormFieldKind.Labels,
            FormValue.OfChosen(["a", "b"]),
            FormValue.OfChosen(["a", "c"]));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, new EditRequest("A-1", [change])));

        Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Equal("PUT", fixture.Http.Methods.First());
        Assert.EndsWith("/rest/api/3/issue/A-1", fixture.Http.Requests.First().AbsolutePath);
        var body = fixture.Http.Bodies.First().Replace(" ", string.Empty);
        Assert.Contains("\"update\":{\"labels\":[{\"add\":\"b\"},{\"remove\":\"c\"}]}", body);
        Assert.DoesNotContain("\"fields\"", body);
    }

    [Fact]
    public async Task GivenAFieldJiraRefuses_WhenExecuted_ThenTheFailureIsDefiniteAndNamesTheField()
    {
        const string body = """{"errorMessages":[],"errors":{"labels":"Label is invalid"}}""";
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.BadRequest, body));
        var change = new FieldChange("labels", FormFieldKind.Labels, FormValue.OfChosen(["a"]));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, new EditRequest("A-1", [change])));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal("labels", Assert.Single(failed.Error.FieldErrors).FieldId);
    }

    [Fact]
    public async Task GivenANumberThatDoesNotRead_WhenExecuted_ThenNothingIsSentAndItFailsDefinitely()
    {
        using var fixture = await ConnectedAsync(_ => { });
        var change = new FieldChange("customfield_1", FormFieldKind.Number, FormValue.OfText("12x"));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, new EditRequest("A-1", [change])));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(WriteErrorKind.Validation, failed.Error.Kind);
        Assert.Empty(fixture.Http.Requests);
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
            Metadata = new FormMetadataHandler(Runner);
            Writes = new WriteHandler(Runner);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public FormMetadataHandler Metadata { get; }

        public WriteHandler Writes { get; }

        public List<Command> Posted { get; } = [];

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }
    }
}
