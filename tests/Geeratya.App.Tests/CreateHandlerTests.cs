using System.Net;
using System.Text.Json;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Forms;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes.Models;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public sealed class CreateHandlerTests
{
    private const string CreatedReply = """{"id":"10049","key":"A-9","self":"https://a.atlassian.net/rest/api/3/issue/10049"}""";

    private const string FoundRow =
        """{"issues":[{"id":"10049","key":"A-9","fields":{"summary":"Title","issuetype":{"id":"10010","name":"Task"},"project":{"key":"GCM"}}}],"isLast":true}""";

    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly TabId _tab = new(1);

    [Theory]
    [InlineData("GCM", "createmeta-issuetypes-gcm.json", "10000")]
    [InlineData("GTM", "createmeta-issuetypes-gtm.json", "10012")]
    public async Task GivenEitherProjectKind_WhenTheFormOpens_ThenTheTypesOfThatProjectAreRead(
        string project,
        string fixtureName,
        string epicId)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, RecordedFixtures.Read($"metadata/{fixtureName}")));

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, new CreateFormTarget(project, null, false, null)));

        var loaded = Assert.IsType<FormTypesLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(7, loaded.RequestId);
        Assert.Equal(5, loaded.Types.Count);
        Assert.Contains(loaded.Types, type => type.Id == epicId && type.HierarchyLevel == 1);
        Assert.Single(loaded.Types, type => type.IsSubtask);
        Assert.EndsWith($"/rest/api/3/issue/createmeta/{project}/issuetypes", fixture.Http.Requests.Single().AbsolutePath);
    }

    [Theory]
    [InlineData("GCM", "10007", "createmeta-fields-gcm-subtask.json")]
    [InlineData("GTM", "10011", "createmeta-fields-gtm-subtask.json")]
    public async Task GivenASubtaskType_WhenTheFieldsLoad_ThenTheParentIsPrefilledAndSearchable(
        string project,
        string typeId,
        string fixtureName)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, RecordedFixtures.Read($"metadata/{fixtureName}")));
        var type = new CreateIssueType(typeId, "Sub-task", string.Empty, -1, true);

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 8, new CreateFormTarget(project, $"{project}-6", true, type)));

        var loaded = Assert.IsType<FormMetadataLoaded>(Assert.Single(fixture.Posted));
        var parent = loaded.Fields.Single(field => field.Id == "parent");
        Assert.Contains(loaded.Fields, field => field is { Id: "summary", Kind: FormFieldKind.Text });
        Assert.Equal([$"{project}-6"], parent.Value.Chosen);
        Assert.True(parent.IsEditable);
        Assert.DoesNotContain(loaded.Fields, field => field.Id is "project" or "issuetype");
        Assert.EndsWith($"/createmeta/{project}/issuetypes/{typeId}", fixture.Http.Requests.Single().AbsolutePath);
    }

    [Fact]
    public async Task GivenATeamManagedTask_WhenTheFieldsLoad_ThenTheRequiredReporterIsAPersonPickerAndNotText()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/createmeta-fields-gtm-task.json")));
        var type = new CreateIssueType("10015", "Task", string.Empty, 0, false);

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 8, new CreateFormTarget("GTM", null, false, type)));

        var loaded = Assert.IsType<FormMetadataLoaded>(Assert.Single(fixture.Posted));
        var reporter = loaded.Fields.Single(field => field.Id == "reporter");
        Assert.True(reporter.Required);
        Assert.Equal(FormFieldKind.User, reporter.Kind);
    }

    [Fact]
    public async Task GivenAMissingProject_WhenTheTypesLoad_ThenTheFailureNamesTheProject()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NotFound, "{}"));

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(_tab, 7, new CreateFormTarget("GCM", null, false, null)));

        Assert.Equal("GCM not found or no access", Assert.IsType<FormMetadataFailed>(Assert.Single(fixture.Posted)).Message);
    }

    [Fact]
    public async Task GivenACompanyManagedSubtask_WhenExecuted_ThenOnePostCarriesProjectTypeParentAndMarkdownAsAdf()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.Created, CreatedReply)
            .Reply(HttpStatusCode.OK, FoundRow));
        var request = Request(
            "GCM",
            "10007",
            "GCM-6",
            Change("summary", FormFieldKind.Text, FormValue.OfText("Wire it")),
            Change("description", FormFieldKind.Paragraph, FormValue.OfText("# Plan\n\n- one")),
            Change("labels", FormFieldKind.Labels, FormValue.OfChosen(["x", "y"])),
            Change("components", FormFieldKind.MultiSelect, FormValue.OfChosen(["31"])),
            Change("priority", FormFieldKind.Select, FormValue.OfChosen(["3"])));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, request));

        var succeeded = Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Equal("A-9", succeeded.CreatedKey);
        Assert.Equal(["POST", "GET"], fixture.Http.Methods);
        using var body = JsonDocument.Parse(fixture.Http.Bodies.First());
        var fields = body.RootElement.GetProperty("fields");
        Assert.Equal("GCM", fields.GetProperty("project").GetProperty("key").GetString());
        Assert.Equal("10007", fields.GetProperty("issuetype").GetProperty("id").GetString());
        Assert.Equal("GCM-6", fields.GetProperty("parent").GetProperty("key").GetString());
        Assert.Equal("Wire it", fields.GetProperty("summary").GetString());
        Assert.Equal("heading", fields.GetProperty("description").GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Equal("bulletList", fields.GetProperty("description").GetProperty("content")[1].GetProperty("type").GetString());
        Assert.Equal("y", fields.GetProperty("labels")[1].GetString());
        Assert.Equal("31", fields.GetProperty("components")[0].GetProperty("id").GetString());
        Assert.Equal("3", fields.GetProperty("priority").GetProperty("id").GetString());
    }

    [Fact]
    public async Task GivenATeamManagedEpic_WhenExecuted_ThenNoParentIsSentAndTheKeyComesFromJira()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.Created, RecordedFixtures.Read("writes/create-gtm-task-reply.json"))
            .Reply(HttpStatusCode.OK, """{"issues":[],"isLast":true}"""));
        var request = Request("GTM", "10012", null, Change("summary", FormFieldKind.Text, FormValue.OfText("Big thing")));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, request));

        var succeeded = Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Equal("GTM-25", succeeded.CreatedKey);
        Assert.Empty(succeeded.Rows);
        Assert.DoesNotContain("\"parent\"", fixture.Http.Bodies.First());
    }

    [Fact]
    public async Task GivenJiraRefusesTheParent_WhenExecuted_ThenTheFailureIsDefiniteAndNamesTheField()
    {
        var refusal = RecordedFixtures.Read("create/synthetic-create-400-invalid-parent.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.BadRequest, refusal));
        var request = Request("GCM", "10010", "GCM-6", Change("summary", FormFieldKind.Text, FormValue.OfText("Wire it")));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, request));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal("parent", Assert.Single(failed.Error.FieldErrors).FieldId);
        Assert.Equal(["POST"], fixture.Http.Methods);
    }

    [Fact]
    public async Task GivenAServerError_WhenExecuted_ThenTheOutcomeIsUnknownAndTheCreateIsNotRepeated()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.InternalServerError, "{}"));
        var request = Request("GCM", "10010", null, Change("summary", FormFieldKind.Text, FormValue.OfText("Title")));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, request));

        Assert.IsType<WriteOutcomeUnknown>(Assert.Single(fixture.Posted));
        Assert.Equal(["POST"], fixture.Http.Methods);
    }

    [Fact]
    public async Task GivenALostCreate_WhenVerified_ThenAnIssueOfTheSameTypeAndSummaryMeansItWasCreated()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, FoundRow));
        var request = Request("GCM", "10010", null, Change("summary", FormFieldKind.Text, FormValue.OfText("Title")));

        await fixture.Writes.StartAsync(new VerifyWriteEffect(5, request));

        var verified = Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted));
        Assert.Equal(VerifyOutcome.Applied, verified.Outcome);
        Assert.Equal("A-9", Assert.Single(verified.Rows).Key);
        var query = Uri.UnescapeDataString(fixture.Http.Requests.Single().Query.Replace('+', ' '));
        Assert.Contains("reporter = currentUser() AND created >= -10m", query);
        Assert.Equal(["GET"], fixture.Http.Methods);
    }

    [Fact]
    public async Task GivenALostCreate_WhenTheSearchFindsNothing_ThenTheOutcomeStaysUndecided()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, """{"issues":[],"isLast":true}"""));
        var request = Request("GCM", "10010", null, Change("summary", FormFieldKind.Text, FormValue.OfText("Title")));

        await fixture.Writes.StartAsync(new VerifyWriteEffect(5, request));

        Assert.Equal(VerifyOutcome.Unreachable, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
    }

    [Fact]
    public void GivenEveryFieldKind_WhenBuildingTheNewIssue_ThenEachIsWrittenInItsOwnShape()
    {
        var request = Request(
            "GCM",
            "10010",
            null,
            Change("summary", FormFieldKind.Text, FormValue.OfText("  Title ")),
            Change("customfield_1", FormFieldKind.Number, FormValue.OfText("2.5")),
            Change("duedate", FormFieldKind.Date, FormValue.OfText("2026-12-01")),
            Change("customfield_2", FormFieldKind.DateTime, FormValue.OfText("2026-12-01 09:30")),
            Change("customfield_3", FormFieldKind.Text, FormValue.OfText("plain")));

        var issue = NewIssues.For(request);

        Assert.Equal("Title", issue.Summary);
        Assert.True(issue.DescriptionIsMarkdown);
        Assert.Equal(CreateFieldValueKind.Number, issue.Fields!["customfield_1"].Kind);
        Assert.Equal(["2026-12-01"], issue.Fields["duedate"].Values);
        Assert.StartsWith("2026-12-01T09:30:00.000", issue.Fields["customfield_2"].Values[0], StringComparison.Ordinal);
        Assert.Equal(["plain"], issue.Fields["customfield_3"].Values);
    }

    [Fact]
    public async Task GivenADateThatDoesNotRead_WhenExecuted_ThenNothingIsSentAndItFailsDefinitely()
    {
        using var fixture = await ConnectedAsync(_ => { });
        var request = Request("GCM", "10010", null, Change("duedate", FormFieldKind.Date, FormValue.OfText("tomorrow")));

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(5, request));

        Assert.Equal(WriteErrorKind.Validation, Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted)).Error.Kind);
        Assert.Empty(fixture.Http.Requests);
    }

    private static CreateRequest Request(string project, string typeId, string? parent, params FieldChange[] changes)
    {
        return new CreateRequest(project, typeId, "Type", parent, changes, false);
    }

    private static FieldChange Change(string id, FormFieldKind kind, FormValue value)
    {
        return new FieldChange(id, kind, value);
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
