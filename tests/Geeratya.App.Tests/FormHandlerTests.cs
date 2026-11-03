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
    private static readonly string[] _knownIds = ["summary", "labels", "priority"];

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

    [Fact]
    public async Task GivenAnAssignment_WhenExecuted_ThenOnePutNamesTheAccountAndUnassigningSendsNull()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, """{"issues":[],"isLast":true}""")
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, """{"issues":[],"isLast":true}"""));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, new AssignRequest("A-1", new("acc-eve", "Eve"))));
        await fixture.Writes.StartAsync(new ExecuteWriteEffect(6, new AssignRequest("A-1", null)));

        Assert.Equal("PUT", fixture.Http.Methods.ElementAt(0));
        Assert.EndsWith("/rest/api/3/issue/A-1/assignee", fixture.Http.Requests.First().AbsolutePath);
        Assert.Contains("\"accountId\":\"acc-eve\"", fixture.Http.Bodies.ElementAt(0).Replace(" ", string.Empty));
        Assert.Contains("\"accountId\":null", fixture.Http.Bodies.ElementAt(2).Replace(" ", string.Empty));
    }

    [Fact]
    public async Task GivenAPriorityChoice_WhenExecuted_ThenOnePutSetsThePriorityById()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, """{"issues":[],"isLast":true}"""));
        var change = new FieldChange("priority", FormFieldKind.Select, FormValue.OfChosen(["2"]), null, ["High"]);

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, new EditRequest("A-1", [change])));

        Assert.Contains("\"priority\":{\"id\":\"2\"}", fixture.Http.Bodies.ElementAt(0).Replace(" ", string.Empty));
    }

    [Theory]
    [InlineData("metadata/editmeta-gcm-26.json", "issues/field-values-gcm-26.json", true)]
    [InlineData("metadata/editmeta-gtm-40.json", "issues/field-values-gtm-40.json", false)]
    public async Task GivenEditableFieldsWithNoKnownValue_WhenTheFormLoads_ThenTheIssueIsReadForExactlyThoseIds(
        string metadataFixture,
        string valuesFixture,
        bool hasComponents)
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.OK, RecordedFixtures.Read(metadataFixture))
            .Reply(HttpStatusCode.OK, RecordedFixtures.Read(valuesFixture)));

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, KnownTarget()));

        var loaded = Assert.IsType<FormMetadataLoaded>(Assert.Single(fixture.Posted));
        var byId = loaded.Fields.ToDictionary(field => field.Id);
        var read = fixture.Http.Requests.ElementAt(1);
        var asked = Uri.UnescapeDataString(read.Query).TrimStart('?').Replace("fields=", string.Empty).Split(',');
        Assert.EndsWith("/rest/api/3/issue/A-1", read.AbsolutePath);
        Assert.DoesNotContain("summary", asked);
        Assert.DoesNotContain("labels", asked);
        Assert.DoesNotContain("priority", asked);
        Assert.DoesNotContain("assignee", asked);
        Assert.Contains("duedate", asked);
        Assert.Equal("2026-12-31", byId["duedate"].Value.Text);
        Assert.Equal("2026-11-15", byId["customfield_10015"].Value.Text);
        Assert.Equal(FormValue.Empty, byId["customfield_10021"].Value);
        if (hasComponents)
        {
            Assert.Equal(["10000"], byId["components"].Value.Chosen);
            Assert.Equal(["10000"], byId["fixVersions"].Value.Chosen);
            Assert.Equal(["10000"], byId["fixVersions"].Initial.Chosen);
        }

        Assert.Equal(hasComponents, byId.ContainsKey("components"));
        Assert.Equal(hasComponents, asked.Contains("components"));
        Assert.DoesNotContain("customfield_10000", asked);
    }

    [Fact]
    public async Task GivenTheIssueReadFails_WhenTheFormLoads_ThenOnlyTheKnownFieldsAreOffered()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/editmeta-gcm-1.json"))
            .Reply(HttpStatusCode.InternalServerError, "{}"));

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, KnownTarget()));

        var loaded = Assert.IsType<FormMetadataLoaded>(Assert.Single(fixture.Posted));
        var editable = loaded.Fields.Where(field => field.IsEditable).Select(field => field.Id);
        Assert.Equivalent(_knownIds, editable);
    }

    [Fact]
    public async Task GivenOnlyThePriorityIsRequested_WhenTheFormLoads_ThenNoIssueReadGoesOut()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/editmeta-gcm-1.json")));
        var target = KnownTarget() with { OnlyId = "priority" };

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, target));

        Assert.Single(fixture.Http.Requests);
    }

    [Theory]
    [InlineData(
        "metadata/synthetic-editmeta-gcs-story.json",
        "issues/synthetic-field-values-gcs-estimate.json",
        "customfield_10047",
        "5")]
    [InlineData(
        "metadata/synthetic-editmeta-gts-task.json",
        "issues/synthetic-field-values-gts-estimate.json",
        "customfield_10016",
        "2.5")]
    public async Task GivenEstimateOnlyOnEitherBoardKind_WhenTheFormLoads_ThenOnlyThatEstimateFieldIsOffered(
        string metadataFixture,
        string valuesFixture,
        string fieldId,
        string value)
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.OK, RecordedFixtures.Read(metadataFixture))
            .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
            .Reply(HttpStatusCode.OK, RecordedFixtures.Read(valuesFixture)));

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, EstimateTarget()));

        var loaded = Assert.IsType<FormMetadataLoaded>(Assert.Single(fixture.Posted));
        var field = Assert.Single(loaded.Fields);
        Assert.Equal(fieldId, field.Id);
        Assert.Equal(FormFieldKind.Number, field.Kind);
        Assert.Equal(value, field.Value.Text);
        var read = fixture.Http.Requests.Last();
        Assert.Equal($"fields={fieldId}", Uri.UnescapeDataString(read.Query).TrimStart('?'));
    }

    [Fact]
    public async Task GivenAnIssueWithoutAnEstimateOnItsScreen_WhenEstimateOnlyLoads_ThenItFailsSayingWhyAndReadsNoValues()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/synthetic-editmeta-gcs-task.json"))
            .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json")));

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, EstimateTarget()));

        var failed = Assert.IsType<FormMetadataFailed>(Assert.Single(fixture.Posted));
        Assert.Contains("A-1 has no estimate to edit", failed.Message);
        Assert.Equal(2, fixture.Http.Requests.Count);
    }

    [Fact]
    public async Task GivenAnEstimateEdit_WhenExecuted_ThenOnePutSetsTheNumberAndAnEmptyTextSendsNull()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, """{"issues":[],"isLast":true}""")
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, """{"issues":[],"isLast":true}"""));

        var set = new FieldChange("customfield_10047", FormFieldKind.Number, FormValue.OfText("8"));
        var clear = new FieldChange("customfield_10047", FormFieldKind.Number, FormValue.Empty);
        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, new EditRequest("A-1", [set], true)));
        await fixture.Writes.StartAsync(new ExecuteWriteEffect(6, new EditRequest("A-1", [clear], true)));

        var bodies = fixture.Http.Bodies.Select(body => body.Replace(" ", string.Empty)).ToArray();
        Assert.Contains("\"fields\":{\"customfield_10047\":8", bodies[0]);
        Assert.Contains("\"customfield_10047\":null", bodies[2]);
    }

    private static EditFormTarget EstimateTarget()
    {
        return new EditFormTarget("A-1", new Dictionary<string, FormValue>(), true, FormFieldKind.Number, EstimateOnly: true);
    }

    private static EditFormTarget KnownTarget()
    {
        var known = new Dictionary<string, FormValue>
        {
            ["summary"] = FormValue.OfText("Title"),
            ["labels"] = FormValue.OfChosen(["a"]),
            ["priority"] = FormValue.OfText("High"),
        };

        return new EditFormTarget("A-1", known, true, null);
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
