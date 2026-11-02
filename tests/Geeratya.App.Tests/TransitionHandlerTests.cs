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
public sealed class TransitionHandlerTests
{
    private const string Moved =
        """
        {"issues":[{"id":"1","key":"A-1","fields":{"summary":"One","status":{"id":"3","name":"In Progress","statusCategory":{"key":"indeterminate"}}}}],"isLast":true}
        """;

    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly TabId _tab = new(1);

    [Fact]
    public async Task GivenTheRecordedTransitions_WhenThePickerLoads_ThenEveryTransitionIsAChoiceAndDoneAsksFirst()
    {
        var recorded = RecordedFixtures.Read("transitions/transitions-gcm-1.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, recorded));
        var target = new TransitionPickTarget("GCM-1", [], null, []);

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, target));

        var loaded = Assert.IsType<FormMetadataLoaded>(Assert.Single(fixture.Posted));
        var refined = Assert.IsType<TransitionPickTarget>(loaded.Target);
        Assert.Equal(["Backlog", "To Do", "In Progress", "Done"], loaded.Fields[0].Options.Select(o => o.Label));
        Assert.Equal(["11", "21", "31", "41"], refined.Choices.Select(choice => choice.Id));
        Assert.Equal([false, false, false, true], refined.Choices.Select(choice => choice.AsksFirst));
        Assert.EndsWith("/rest/api/3/issue/GCM-1/transitions", fixture.Http.Requests.Single().AbsolutePath);
    }

    [Fact]
    public async Task GivenATransitionWithARequiredResolution_WhenItsFormLoads_ThenTheFieldIsRequiredWithItsChoices()
    {
        var synthetic = RecordedFixtures.Read("writes/synthetic-transitions-required-field.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, synthetic));
        var choice = new TransitionChoice("41", "Done", new("10005", "Done", "done"), true, false);

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, new TransitionFormTarget("A-1", choice)));

        var loaded = Assert.IsType<FormMetadataLoaded>(Assert.Single(fixture.Posted));
        var field = Assert.Single(loaded.Fields);
        Assert.Equal("resolution", field.Id);
        Assert.True(field.Required);
        Assert.Equal(["10000", "10001"], field.Options.Select(option => option.Id));
    }

    [Fact]
    public async Task GivenTheTeamManagedTransitions_WhenThePickerLoads_ThenTheGlobalTransitionsAreChoices()
    {
        var recorded = RecordedFixtures.Read("transitions/transitions-gtm-5.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, recorded));
        var target = new TransitionPickTarget("GTM-5", [], null, []);

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, target));

        var loaded = Assert.IsType<FormMetadataLoaded>(Assert.Single(fixture.Posted));
        var refined = Assert.IsType<TransitionPickTarget>(loaded.Target);
        Assert.Equal(["11", "21", "31", "41"], refined.Choices.Select(choice => choice.Id).Order());
        Assert.All(refined.Choices, choice => Assert.False(choice.NeedsForm));
        Assert.EndsWith("/rest/api/3/issue/GTM-5/transitions", fixture.Http.Requests.Single().AbsolutePath);
    }

    [Fact]
    public async Task GivenAScreenWithOnlyAFieldTheFormCannotEdit_WhenItsFormLoads_ThenItSaysToUseTheBrowser()
    {
        const string screen = """
            {"transitions":[{"id":"31","name":"Start","hasScreen":true,"isAvailable":true,"isGlobal":true,
            "to":{"id":"3","name":"In Progress","statusCategory":{"key":"indeterminate"}},
            "fields":{"customfield_1":{"required":true,"name":"Reviewer","fieldId":"customfield_1",
            "schema":{"type":"any"},"hasDefaultValue":false}}}]}
            """;
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, screen));
        var choice = new TransitionChoice("31", "Start", new("3", "In Progress", "indeterminate"), true, false);

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, new TransitionFormTarget("A-1", choice)));

        var failed = Assert.IsType<FormMetadataFailed>(Assert.Single(fixture.Posted));
        Assert.Contains("do it in the browser", failed.Message);
    }

    [Fact]
    public async Task GivenATransitionThatNoLongerExists_WhenItsFormLoads_ThenTheFormFailsWithTheReason()
    {
        var recorded = RecordedFixtures.Read("transitions/transitions-gcm-1.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, recorded));
        var choice = new TransitionChoice("99", "Gone", new("1", "Gone", "new"), true, false);

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, new TransitionFormTarget("A-1", choice)));

        var failed = Assert.IsType<FormMetadataFailed>(Assert.Single(fixture.Posted));
        Assert.Equal("Gone is no longer available for A-1", failed.Message);
    }

    [Fact]
    public async Task GivenAPlainMove_WhenExecuted_ThenOnePostCarriesTheTransitionIdAndTheRowIsReadBack()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, Moved));
        var request = new TransitionRequest("A-1", Choice("31", "3", "In Progress"), [], false);

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, request));

        var succeeded = Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Equal("In Progress", Assert.Single(succeeded.Rows).Status.Name);
        Assert.Equal(["POST", "GET"], fixture.Http.Methods);
        Assert.EndsWith("/rest/api/3/issue/A-1/transitions", fixture.Http.Requests.First().AbsolutePath);
        Assert.Equal("""{"transition":{"id":"31"}}""", fixture.Http.Bodies.First().Replace(" ", string.Empty));
    }

    [Fact]
    public async Task GivenAFormWithAResolution_WhenExecuted_ThenTheFieldIsSentAsAnIdObjectUnderFields()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, """{"issues":[],"isLast":true}"""));
        var change = new FieldChange("resolution", FormFieldKind.Select, FormValue.OfChosen(["10000"]));
        var request = new TransitionRequest("A-1", Choice("41", "10005", "Done"), [change], false);

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, request));

        Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        var body = fixture.Http.Bodies.First().Replace(" ", string.Empty);
        Assert.Equal("""{"transition":{"id":"41"},"fields":{"resolution":{"id":"10000"}}}""", body);
    }

    [Fact]
    public async Task GivenAMissingRequiredFieldAtJira_WhenExecuted_ThenTheFailureIsDefiniteAndNamesTheField()
    {
        var refusal = RecordedFixtures.Read("writes/synthetic-transition-400-required-field.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.BadRequest, refusal));
        var request = new TransitionRequest("A-1", Choice("41", "10005", "Done"), [], false);

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, request));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal("resolution", Assert.Single(failed.Error.FieldErrors).FieldId);
    }

    [Fact]
    public async Task GivenAForbiddenMove_WhenExecuted_ThenNoReadFollowsAndTheFailureIsDefinite()
    {
        const string forbidden = """{"errorMessages":["No"]}""";
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.Forbidden, forbidden));
        var request = new TransitionRequest("A-1", Choice("31", "3", "In Progress"), [], false);

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, request));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(WriteErrorKind.Permission, failed.Error.Kind);
        Assert.Single(fixture.Http.Requests);
    }

    private static TransitionChoice Choice(string id, string statusId, string name)
    {
        return new TransitionChoice(id, name, new(statusId, name, "indeterminate"), false, false);
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
