using System.Net;
using System.Text.Json;
using Geeratya.Jira.Create;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Create;

public sealed class IssueCreateClientTests
{
    private const string MetaPath = "/rest/api/3/issue/createmeta";
    private const string IssuePath = "/rest/api/3/issue";

    [Theory]
    [InlineData("GCM", "createmeta-issuetypes-gcm", "Sub-task")]
    [InlineData("GTM", "createmeta-issuetypes-gtm", "Subtask")]
    public async Task GivenEitherProjectKind_WhenGettingIssueTypes_ThenHierarchyAndSubtaskFlagAreMapped(
        string project,
        string fixtureName,
        string subtaskName)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"{MetaPath}/{project}/issuetypes",
            FakeResponse.Ok(FixtureFiles.Read($"metadata/{fixtureName}.json")));

        var result = await new IssueCreateClient(fixture.Transport)
            .GetIssueTypesAsync(project, CancellationToken.None);

        var types = result.Value;
        Assert.Equal(5, types.Count);
        Assert.Equal(1, types.Single(type => type.Name == "Epic").HierarchyLevel);
        var subtask = types.Single(type => type.IsSubtask);
        Assert.Equal(subtaskName, subtask.Name);
        Assert.Equal(-1, subtask.HierarchyLevel);
        Assert.Equal(0, types.Single(type => type.Name == "Story").HierarchyLevel);
        Assert.Contains("startAt=0", fixture.Handler.Requests.Single().PathAndQuery);
    }

    [Fact]
    public async Task GivenSyntheticCustomTypes_WhenGettingIssueTypes_ThenLevelsComeFromJiraAndIdlessEntriesAreDropped()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"{MetaPath}/GCM/issuetypes",
            FakeResponse.Ok(FixtureFiles.Read("create/synthetic-issuetypes-custom.json")));

        var result = await new IssueCreateClient(fixture.Transport)
            .GetIssueTypesAsync("GCM", CancellationToken.None);

        var types = result.Value;
        Assert.Equal(["Initiative", "Chore", "Mikro-uzduotis"], types.Select(type => type.Name));
        Assert.Equal(2, types[0].HierarchyLevel);
        Assert.False(types[0].IsSubtask);
        Assert.Equal(string.Empty, types[1].Description);
        Assert.True(types[2].IsSubtask);
    }

    [Fact]
    public async Task GivenTotalLargerThanFirstPage_WhenGettingIssueTypes_ThenNextPageIsFetched()
    {
        using var fixture = new TransportFixture();
        var path = $"{MetaPath}/GCM/issuetypes";
        fixture.Handler.Script(
            HttpMethod.Get,
            path,
            FakeResponse.Ok("""{"startAt":0,"maxResults":1,"total":2,"issueTypes":[{"id":"1","name":"A"}]}"""),
            FakeResponse.Ok("""{"startAt":1,"maxResults":1,"total":2,"issueTypes":[{"id":"2","name":"B"}]}"""));

        var result = await new IssueCreateClient(fixture.Transport)
            .GetIssueTypesAsync("GCM", CancellationToken.None);

        Assert.Equal(["A", "B"], result.Value.Select(type => type.Name));
        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Contains("startAt=1", fixture.Handler.Requests.Last().PathAndQuery);
    }

    [Fact]
    public async Task GivenEmptyPageDespiteTotal_WhenGettingIssueTypes_ThenWalkEnds()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"{MetaPath}/GCM/issuetypes",
            FakeResponse.Ok("""{"startAt":0,"maxResults":50,"total":99,"issueTypes":[]}"""));

        var result = await new IssueCreateClient(fixture.Transport)
            .GetIssueTypesAsync("GCM", CancellationToken.None);

        Assert.Empty(result.Value);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenMalformedProjectKey_WhenGettingMetadata_ThenNotFoundWithoutRequest()
    {
        using var fixture = new TransportFixture();
        var client = new IssueCreateClient(fixture.Transport);

        var types = await client.GetIssueTypesAsync("../x", CancellationToken.None);
        var fields = await client.GetFieldsAsync("GCM", "10010/../x", CancellationToken.None);

        Assert.Equal(JiraErrorKind.NotFound, types.Error.Kind);
        Assert.Equal(JiraErrorKind.NotFound, fields.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenProjectWithoutPermission_WhenGettingIssueTypes_ThenForbidden()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"{MetaPath}/GCM/issuetypes",
            FakeResponse.WithStatus(HttpStatusCode.Forbidden, """{"errorMessages":["No."],"errors":{}}"""));

        var result = await new IssueCreateClient(fixture.Transport)
            .GetIssueTypesAsync("GCM", CancellationToken.None);

        Assert.Equal(JiraErrorKind.Forbidden, result.Error.Kind);
    }

    [Theory]
    [InlineData("GCM", "10010", "gcm-task", CreateParentRule.Optional)]
    [InlineData("GCM", "10007", "gcm-subtask", CreateParentRule.Required)]
    [InlineData("GCM", "10000", "gcm-epic", CreateParentRule.NotAllowed)]
    [InlineData("GTM", "10015", "gtm-task", CreateParentRule.Optional)]
    [InlineData("GTM", "10011", "gtm-subtask", CreateParentRule.Required)]
    [InlineData("GTM", "10012", "gtm-epic", CreateParentRule.NotAllowed)]
    public async Task GivenRecordedFieldsOfEachKindAndType_WhenGettingFields_ThenParentRuleFollowsMetadata(
        string project,
        string typeId,
        string fixtureName,
        CreateParentRule expected)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"{MetaPath}/{project}/issuetypes/{typeId}",
            FakeResponse.Ok(FixtureFiles.Read($"metadata/createmeta-fields-{fixtureName}.json")));

        var result = await new IssueCreateClient(fixture.Transport)
            .GetFieldsAsync(project, typeId, CancellationToken.None);

        Assert.Equal(expected, CreateFormRules.ParentRule(result.Value));
        Assert.True(result.Value.Single(field => field.FieldId == "summary").Required);
    }

    [Fact]
    public async Task GivenRecordedFields_WhenGettingFields_ThenKindsAndChoicesAreMapped()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"{MetaPath}/GCM/issuetypes/10010",
            FakeResponse.Ok(FixtureFiles.Read("metadata/createmeta-fields-gcm-task.json")));

        var result = await new IssueCreateClient(fixture.Transport)
            .GetFieldsAsync("GCM", "10010", CancellationToken.None);

        var fields = result.Value.ToDictionary(field => field.FieldId);
        Assert.Equal(CreateFieldKind.Text, fields["summary"].Kind);
        Assert.Equal(CreateFieldKind.RichText, fields["description"].Kind);
        Assert.Equal(CreateFieldKind.User, fields["assignee"].Kind);
        Assert.Equal(CreateFieldKind.Labels, fields["labels"].Kind);
        Assert.Equal(CreateFieldKind.Date, fields["duedate"].Kind);
        Assert.Equal(CreateFieldKind.MultiChoice, fields["components"].Kind);
        Assert.Equal(CreateFieldKind.Fixed, fields["project"].Kind);
        Assert.Equal(CreateFieldKind.Parent, fields["parent"].Kind);
        Assert.Equal(CreateFieldKind.Unsupported, fields["attachment"].Kind);
        Assert.Equal(CreateFieldKind.Unsupported, fields["customfield_10001"].Kind);
        Assert.Equal(CreateFieldKind.SingleChoice, fields["priority"].Kind);
        Assert.Equal(5, fields["priority"].AllowedValues.Count);
        Assert.Contains(fields["priority"].AllowedValues, value => value.Label == "Highest");
    }

    [Fact]
    public async Task GivenTeamManagedFields_WhenGettingFields_ThenReporterIsRequiredWithDefault()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"{MetaPath}/GTM/issuetypes/10015",
            FakeResponse.Ok(FixtureFiles.Read("metadata/createmeta-fields-gtm-task.json")));

        var result = await new IssueCreateClient(fixture.Transport)
            .GetFieldsAsync("GTM", "10015", CancellationToken.None);

        var reporter = result.Value.Single(field => field.FieldId == "reporter");
        Assert.True(reporter.Required);
        Assert.True(reporter.HasDefaultValue);
        var missing = CreateFormRules.MissingRequired(result.Value, new NewIssue("GTM", "10015", "Do it"));
        Assert.Empty(missing);
    }

    [Fact]
    public async Task GivenSyntheticFields_WhenGettingFields_ThenUnknownTypesStayListedAndOddChoicesAreSkipped()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"{MetaPath}/GCM/issuetypes/20003",
            FakeResponse.Ok(FixtureFiles.Read("create/synthetic-fields-custom.json")));

        var result = await new IssueCreateClient(fixture.Transport)
            .GetFieldsAsync("GCM", "20003", CancellationToken.None);

        var fields = result.Value.ToDictionary(field => field.FieldId);
        Assert.Equal(12, fields.Count);
        Assert.Equal(CreateFieldKind.Unsupported, fields["customfield_20013"].Kind);
        Assert.Equal(CreateFieldKind.Unsupported, fields["customfield_20014"].Kind);
        Assert.Equal("Hologram", fields["customfield_20013"].Name);
        Assert.Equal(CreateFieldKind.RichText, fields["customfield_20011"].Kind);
        Assert.Equal(CreateFieldKind.MultiChoice, fields["customfield_20012"].Kind);

        var risk = fields["customfield_20010"];
        Assert.Equal(["30001", "30002"], risk.AllowedValues.Select(value => value.Id));
        Assert.Equal(["Low", "High"], risk.AllowedValues.Select(value => value.Label));
    }

    [Fact]
    public async Task GivenSyntheticRequiredFields_WhenCheckingMissing_ThenOnlyUnfilledNonDefaultOnesAreListed()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"{MetaPath}/GCM/issuetypes/20003",
            FakeResponse.Ok(FixtureFiles.Read("create/synthetic-fields-custom.json")));
        var fields = (await new IssueCreateClient(fixture.Transport)
            .GetFieldsAsync("GCM", "20003", CancellationToken.None)).Value;

        var bare = CreateFormRules.MissingRequired(fields, new NewIssue("GCM", "20003", "Title"));
        var filled = CreateFormRules.MissingRequired(
            fields,
            new NewIssue(
                "GCM",
                "20003",
                "Title",
                ParentKey: "GCM-5",
                Fields: new Dictionary<string, CreateFieldValue>
                {
                    ["customfield_20010"] = CreateFieldValue.Choice("30001"),
                    ["customfield_20015"] = CreateFieldValue.Text("x"),
                }));

        Assert.Equal(["parent", "customfield_20010", "customfield_20015"], bare.Select(field => field.FieldId));
        Assert.Empty(filled);
    }

    [Fact]
    public void GivenBlankSummary_WhenCheckingMissing_ThenSummaryIsListed()
    {
        var fields = new[]
        {
            new CreateField("summary", "Summary", true, false, CreateFieldKind.Text, [], null),
        };

        var missing = CreateFormRules.MissingRequired(fields, new NewIssue("GCM", "10010", "  "));

        Assert.Equal("summary", Assert.Single(missing).FieldId);
    }

    [Fact]
    public async Task GivenGtmTaskWithDescription_WhenCreating_ThenBodyHasProjectTypeSummaryAndAdf()
    {
        using var fixture = ServingCreate("writes/create-gtm-task-reply.json");

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("GTM", "10015", "  Fix login  ", "First line\n\nThird, \"quoted\" é"),
            CancellationToken.None);

        Assert.Equal("GTM-25", result.Value.Key);
        Assert.Equal("10049", result.Value.Id);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        using var body = JsonDocument.Parse(request.Body!);
        var fields = body.RootElement.GetProperty("fields");
        Assert.Equal("GTM", fields.GetProperty("project").GetProperty("key").GetString());
        Assert.Equal("10015", fields.GetProperty("issuetype").GetProperty("id").GetString());
        Assert.Equal("Fix login", fields.GetProperty("summary").GetString());
        Assert.False(fields.TryGetProperty("parent", out _));
        var description = fields.GetProperty("description");
        Assert.Equal(1, description.GetProperty("version").GetInt32());
        Assert.Equal("doc", description.GetProperty("type").GetString());
        var paragraphs = description.GetProperty("content");
        Assert.Equal(3, paragraphs.GetArrayLength());
        Assert.Equal("First line", paragraphs[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.False(paragraphs[1].TryGetProperty("content", out _));
        Assert.Equal("Third, \"quoted\" é", paragraphs[2].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("GCM", "10007", "GCM-5", "hierarchy/create-gcm-subtask.json", "GCM-6")]
    [InlineData("GTM", "10011", "GTM-4", "hierarchy/create-gtm-subtask.json", null)]
    public async Task GivenSubtaskOfEitherKind_WhenCreating_ThenParentIsSetByKey(
        string project,
        string typeId,
        string parent,
        string replyFixture,
        string? expectedKey)
    {
        using var fixture = ServingCreate(replyFixture);

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue(project, typeId, "Child", ParentKey: parent),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        if (expectedKey is not null)
        {
            Assert.Equal(expectedKey, result.Value.Key);
        }

        using var body = JsonDocument.Parse(fixture.Handler.Requests.Single().Body!);
        var fields = body.RootElement.GetProperty("fields");
        Assert.Equal(parent, fields.GetProperty("parent").GetProperty("key").GetString());
        Assert.False(fields.TryGetProperty("description", out _));
    }

    [Fact]
    public async Task GivenExtraFieldsOfEachShape_WhenCreating_ThenEachIsWrittenInItsJiraShape()
    {
        using var fixture = ServingCreate("writes/create-gtm-task-reply.json");
        var extras = new Dictionary<string, CreateFieldValue>
        {
            ["customfield_20010"] = CreateFieldValue.Choice("30001"),
            ["customfield_20012"] = CreateFieldValue.Choices(["30010", "30011"]),
            ["labels"] = CreateFieldValue.Labels(["a", "b"]),
            ["assignee"] = CreateFieldValue.User("acc-1"),
            ["duedate"] = CreateFieldValue.Text("2026-11-30"),
            ["customfield_20011"] = CreateFieldValue.RichText("note"),
            ["customfield_20099"] = CreateFieldValue.Number(1.5m),
        };

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("GTM", "10015", "Many", Fields: extras),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        using var body = JsonDocument.Parse(fixture.Handler.Requests.Single().Body!);
        var fields = body.RootElement.GetProperty("fields");
        Assert.Equal("30001", fields.GetProperty("customfield_20010").GetProperty("id").GetString());
        Assert.Equal("30011", fields.GetProperty("customfield_20012")[1].GetProperty("id").GetString());
        Assert.Equal("b", fields.GetProperty("labels")[1].GetString());
        Assert.Equal("acc-1", fields.GetProperty("assignee").GetProperty("accountId").GetString());
        Assert.Equal("2026-11-30", fields.GetProperty("duedate").GetString());
        Assert.Equal("doc", fields.GetProperty("customfield_20011").GetProperty("type").GetString());
        Assert.Equal(1.5m, fields.GetProperty("customfield_20099").GetDecimal());
    }

    [Fact]
    public async Task GivenRecordedMissingSummaryReply_WhenCreating_ThenValidationNamesTheField()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Post,
            IssuePath,
            FakeResponse.WithStatus(HttpStatusCode.BadRequest, FixtureFiles.Read("writes/create-400-no-summary.json")));

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("GCM", "10010", "x"),
            CancellationToken.None);

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Equal("summary", Assert.Single(result.Error.FieldErrors).FieldId);
    }

    [Theory]
    [InlineData("create/synthetic-create-400-required-custom.json", "customfield_20010")]
    [InlineData("create/synthetic-create-400-invalid-parent.json", "parent")]
    public async Task GivenJiraRefusesTheFields_WhenCreating_ThenFieldErrorIsKeyedByFieldId(
        string fixtureName,
        string field)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Post,
            IssuePath,
            FakeResponse.WithStatus(HttpStatusCode.BadRequest, FixtureFiles.Read(fixtureName)));

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("GCM", "20003", "x", ParentKey: "GCM-1"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.Error.IsOutcomeUnknown);
        Assert.Equal(field, Assert.Single(result.Error.FieldErrors).FieldId);
    }

    [Fact]
    public async Task GivenRecordedBadIssueTypeReply_WhenCreating_ThenIssueTypeFieldIsReported()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Post,
            IssuePath,
            FakeResponse.WithStatus(HttpStatusCode.BadRequest, FixtureFiles.Read("errors/create-400-bad-issuetype.json")));

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("GCM", "99999", "x"),
            CancellationToken.None);

        Assert.Equal("issuetype", Assert.Single(result.Error.FieldErrors).FieldId);
    }

    [Theory]
    [InlineData("", "10010", "GCM-1", null, "summary")]
    [InlineData("  ", "10010", null, null, "summary")]
    [InlineData("x", "10010", "not a key", null, "parent")]
    [InlineData("x", "abc", null, null, "issuetype")]
    [InlineData("x", "10010", null, "summary", "summary")]
    [InlineData("x", "10010", null, "project", "project")]
    public async Task GivenInputThatCannotBeValid_WhenCreating_ThenRefusedWithoutRequest(
        string summary,
        string typeId,
        string? parent,
        string? reservedField,
        string expectedField)
    {
        using var fixture = ServingCreate("writes/create-gtm-task-reply.json");
        var extras = reservedField is null
            ? null
            : new Dictionary<string, CreateFieldValue> { [reservedField] = CreateFieldValue.Text("y") };

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("GCM", typeId, summary, ParentKey: parent, Fields: extras),
            CancellationToken.None);

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Contains(result.Error.FieldErrors, error => error.FieldId == expectedField);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenMalformedProjectKey_WhenCreating_ThenRefusedWithoutRequest()
    {
        using var fixture = ServingCreate("writes/create-gtm-task-reply.json");

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("gcm", "10010", "x"),
            CancellationToken.None);

        Assert.Contains(result.Error.FieldErrors, error => error.FieldId == "project");
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenServerError_WhenCreating_ThenOutcomeIsUnknownAndNotRetried()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, IssuePath, FakeResponse.WithStatus(HttpStatusCode.BadGateway));

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("GCM", "10010", "x"),
            CancellationToken.None);

        Assert.True(result.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenForbiddenCreate_WhenCreating_ThenPermissionError()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Post,
            IssuePath,
            FakeResponse.WithStatus(HttpStatusCode.Forbidden, """{"errorMessages":["No permission."],"errors":{}}"""));

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("GCM", "10010", "x"),
            CancellationToken.None);

        Assert.Equal(WriteErrorKind.Permission, result.Error.Kind);
    }

    [Fact]
    public async Task GivenCreatedReplyWithoutKey_WhenCreating_ThenOutcomeIsUnknown()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Post,
            IssuePath,
            FakeResponse.WithStatus(HttpStatusCode.Created, """{"id":"1"}"""));

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("GCM", "10010", "x"),
            CancellationToken.None);

        Assert.True(result.Error.IsOutcomeUnknown);
    }

    [Fact]
    public async Task GivenRateLimitWithRetryAfter_WhenCreating_ThenSentAgainOnce()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Post,
            IssuePath,
            FakeResponse.RateLimited("2"),
            FakeResponse.WithStatus(HttpStatusCode.Created, FixtureFiles.Read("writes/create-gtm-task-reply.json")));

        var result = await new IssueCreateClient(fixture.Transport).CreateIssueAsync(
            new NewIssue("GTM", "10015", "x"),
            CancellationToken.None);

        Assert.Equal("GTM-25", result.Value.Key);
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenCancelledToken_WhenCreating_ThenCancellationSurfaces()
    {
        using var fixture = ServingCreate("writes/create-gtm-task-reply.json");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new IssueCreateClient(fixture.Transport)
                .CreateIssueAsync(new NewIssue("GTM", "10015", "x"), source.Token));
    }

    private static TransportFixture ServingCreate(string replyFixture)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Post,
            IssuePath,
            FakeResponse.WithStatus(HttpStatusCode.Created, FixtureFiles.Read(replyFixture)));

        return fixture;
    }
}
