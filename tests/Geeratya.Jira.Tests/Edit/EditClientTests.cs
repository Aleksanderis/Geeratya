using System.Net;
using System.Text.Json;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Edit;
using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Edit;

public sealed class EditClientTests
{
    private const string GcmIssuePath = "/rest/api/3/issue/GCM-1";
    private const string GcmMetaPath = "/rest/api/3/issue/GCM-1/editmeta";
    private const string GcmAssigneePath = "/rest/api/3/issue/GCM-1/assignee";
    private const string AccountId = "712020:00000000-0000-0000-0000-000000000001";

    [Fact]
    public async Task GivenRecordedCompanyManagedEditMeta_WhenListing_ThenKindsOperationsAndChoicesAreMapped()
    {
        using var fixture = Serving(GcmMetaPath, "metadata/editmeta-gcm-1.json");

        var result = await new EditClient(fixture.Transport).GetEditFieldsAsync("GCM-1", Cancel());

        var fields = result.Value.ToDictionary(field => field.FieldId);
        Assert.Equal(EditFieldKind.Text, fields["summary"].Kind);
        Assert.Equal(EditFieldKind.RichText, fields["description"].Kind);
        Assert.Equal(EditFieldKind.RichText, fields["environment"].Kind);
        Assert.Equal(EditFieldKind.Date, fields["duedate"].Kind);
        Assert.Equal(EditFieldKind.Option, fields["priority"].Kind);
        Assert.Equal(EditFieldKind.User, fields["assignee"].Kind);
        Assert.Equal(EditFieldKind.Labels, fields["labels"].Kind);
        Assert.Equal(EditFieldKind.MultiOption, fields["components"].Kind);
        Assert.Equal(EditFieldKind.MultiOption, fields["fixVersions"].Kind);
        Assert.Equal(EditFieldKind.MultiOption, fields["customfield_10021"].Kind);
        Assert.Equal("option", fields["customfield_10021"].SchemaItems);
        Assert.Equal(EditFieldKind.Parent, fields["parent"].Kind);
        Assert.Equal(["add", "set", "remove"], fields["labels"].Operations);
        Assert.Equal(5, fields["priority"].AllowedValues.Count);
        Assert.Contains("assignable/search", fields["assignee"].AutoCompleteUrl);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(GcmMetaPath, request.PathAndQuery);
    }

    [Fact]
    public async Task GivenFieldTypesTheAppCannotWrite_WhenListing_ThenTheyStayListedAsReadOnly()
    {
        using var fixture = Serving(GcmMetaPath, "metadata/editmeta-gcm-1.json");

        var result = await new EditClient(fixture.Transport).GetEditFieldsAsync("GCM-1", Cancel());

        var fields = result.Value.ToDictionary(field => field.FieldId);
        Assert.Equal(EditFieldKind.Unsupported, fields["attachment"].Kind);
        Assert.Equal(EditFieldKind.Unsupported, fields["comment"].Kind);
        Assert.Equal(EditFieldKind.Unsupported, fields["issuelinks"].Kind);
        Assert.Equal(EditFieldKind.Unsupported, fields["customfield_10001"].Kind);
        Assert.False(fields["attachment"].IsEditable);
        Assert.False(fields["issuetype"].IsEditable);
        Assert.True(fields["summary"].IsEditable);
    }

    [Fact]
    public async Task GivenSyntheticUnknownTypesAndRenamedChoices_WhenListing_ThenNothingThrowsAndNamesAreKept()
    {
        using var fixture = Serving(GcmMetaPath, "metadata/synthetic-editmeta-unknown-types.json");

        var result = await new EditClient(fixture.Transport).GetEditFieldsAsync("GCM-1", Cancel());

        var fields = result.Value.ToDictionary(field => field.FieldId);
        Assert.Equal(9, fields.Count);
        Assert.Equal(EditFieldKind.Unsupported, fields["customfield_90001"].Kind);
        Assert.Equal("com.vendor:risk-matrix", fields["customfield_90001"].SchemaType);
        Assert.Equal(EditFieldKind.RichText, fields["customfield_90002"].Kind);
        Assert.Equal(EditFieldKind.Number, fields["customfield_90003"].Kind);
        Assert.Equal(EditFieldKind.Unsupported, fields["customfield_90004"].Kind);
        Assert.False(fields["customfield_90005"].IsEditable);
        Assert.Equal(EditFieldKind.DateTime, fields["customfield_90006"].Kind);
        Assert.Equal(EditFieldKind.Unsupported, fields["customfield_90008"].Kind);
        Assert.Equal("No schema", fields["customfield_90008"].Name);
        var priority = fields["priority"];
        Assert.True(priority.HasDefaultValue);
        Assert.Equal(["Renamed Top", "Meh"], priority.AllowedValues.Select(option => option.Label));
    }

    [Fact]
    public async Task GivenRecordedTeamManagedEditMeta_WhenListing_ThenKindsOperationsAndChoicesAreMapped()
    {
        using var fixture = Serving("/rest/api/3/issue/GTM-9/editmeta", "metadata/editmeta-gtm-9.json");

        var result = await new EditClient(fixture.Transport).GetEditFieldsAsync("GTM-9", Cancel());

        var fields = result.Value.ToDictionary(field => field.FieldId);
        Assert.Equal(EditFieldKind.Text, fields["summary"].Kind);
        Assert.True(fields["summary"].Required);
        Assert.Equal(EditFieldKind.RichText, fields["description"].Kind);
        Assert.Equal(EditFieldKind.User, fields["assignee"].Kind);
        Assert.Equal(EditFieldKind.Labels, fields["labels"].Kind);
        Assert.Equal(EditFieldKind.Option, fields["priority"].Kind);
        Assert.Equal(5, fields["priority"].AllowedValues.Count);
        Assert.Equal(["add", "set", "remove"], fields["labels"].Operations);
        Assert.Empty(fields["labels"].AllowedValues);
        Assert.Equal(EditFieldKind.Unsupported, fields["attachment"].Kind);
        Assert.False(fields["issuetype"].IsEditable);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"fields\":null}")]
    public async Task GivenEmptyOrNullEditMeta_WhenListing_ThenEmptyListIsReturned(string body)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, GcmMetaPath, FakeResponse.Ok(body));

        var result = await new EditClient(fixture.Transport).GetEditFieldsAsync("GCM-1", Cancel());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Theory]
    [InlineData("gcm-1")]
    [InlineData("GCM-1/../x")]
    [InlineData("")]
    public async Task GivenInvalidKey_WhenCallingAnything_ThenNothingIsSentAndNotFoundIsReturned(string key)
    {
        using var fixture = new TransportFixture();
        var client = new EditClient(fixture.Transport);

        var meta = await client.GetEditFieldsAsync(key, Cancel());
        var update = await client.UpdateAsync(key, [FieldUpdate.Text("summary", "x")], Cancel());
        var assign = await client.AssignAsync(key, AssigneeChange.Unassign, Cancel());

        Assert.Equal(JiraErrorKind.NotFound, meta.Error.Kind);
        Assert.Equal(WriteErrorKind.NotFound, update.Error.Kind);
        Assert.Equal(WriteErrorKind.NotFound, assign.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenHiddenIssue_WhenListing_ThenNotFoundIsReturned()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/issue-404.json");
        fixture.Handler.Serve(HttpMethod.Get, GcmMetaPath, FakeResponse.WithStatus(HttpStatusCode.NotFound, body));

        var result = await new EditClient(fixture.Transport).GetEditFieldsAsync("GCM-1", Cancel());

        Assert.Equal(JiraErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public async Task GivenTextRichTextAndOptionUpdates_WhenUpdating_ThenOnePutCarriesThemUnderFields()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, GcmIssuePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));
        var description = AdfMarkdownParser.Parse("Hello **bold** 🚀");

        var result = await new EditClient(fixture.Transport).UpdateAsync(
            "GCM-1",
            [
                FieldUpdate.Text("summary", "Zażółć \"gęślą\""),
                FieldUpdate.RichText("description", description),
                FieldUpdate.Option("priority", "2"),
                FieldUpdate.Number("customfield_90003", 3.5),
            ],
            Cancel());

        Assert.True(result.IsSuccess);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        using var sent = JsonDocument.Parse(request.Body!);
        var fields = sent.RootElement.GetProperty("fields");
        Assert.False(sent.RootElement.TryGetProperty("update", out _));
        Assert.Equal("Zażółć \"gęślą\"", fields.GetProperty("summary").GetString());
        Assert.Equal("doc", fields.GetProperty("description").GetProperty("type").GetString());
        Assert.Equal(1, fields.GetProperty("description").GetProperty("version").GetInt32());
        Assert.Equal("2", fields.GetProperty("priority").GetProperty("id").GetString());
        Assert.Equal(3.5, fields.GetProperty("customfield_90003").GetDouble());
    }

    [Fact]
    public async Task GivenClearedFields_WhenUpdating_ThenEachIsSentAsJsonNullNotOmitted()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, GcmIssuePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        await new EditClient(fixture.Transport).UpdateAsync(
            "GCM-1",
            [
                FieldUpdate.Text("customfield_1", null),
                FieldUpdate.RichText("description", null),
                FieldUpdate.Option("customfield_2", null),
                FieldUpdate.User("customfield_3", null),
                FieldUpdate.Date("duedate", null),
                FieldUpdate.DateTime("customfield_4", null),
                FieldUpdate.Number("customfield_5", null),
                FieldUpdate.Parent("parent", null),
                FieldUpdate.Options("components", []),
            ],
            Cancel());

        using var sent = JsonDocument.Parse(Assert.Single(fixture.Handler.Requests).Body!);
        var fields = sent.RootElement.GetProperty("fields");
        Assert.Equal(9, fields.EnumerateObject().Count());
        Assert.All(
            fields.EnumerateObject().Where(property => property.Name != "components"),
            property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
        Assert.Equal(JsonValueKind.Array, fields.GetProperty("components").ValueKind);
        Assert.Equal(0, fields.GetProperty("components").GetArrayLength());
    }

    [Fact]
    public async Task GivenDatesUsersParentAndChoices_WhenUpdating_ThenShapesMatchJirasSchemas()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, GcmIssuePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));
        var moment = new DateTimeOffset(2026, 3, 9, 14, 5, 7, 123, TimeSpan.FromMinutes(-210));

        await new EditClient(fixture.Transport).UpdateAsync(
            "GCM-1",
            [
                FieldUpdate.Date("duedate", new DateOnly(2026, 12, 31)),
                FieldUpdate.DateTime("customfield_90006", moment),
                FieldUpdate.User("customfield_90007", AccountId),
                FieldUpdate.Parent("parent", "GCM-5"),
                FieldUpdate.Options("components", ["10000", "10001"]),
                FieldUpdate.Labels("labels", ["a", "b"]),
            ],
            Cancel());

        using var sent = JsonDocument.Parse(Assert.Single(fixture.Handler.Requests).Body!);
        var fields = sent.RootElement.GetProperty("fields");
        Assert.Equal("2026-12-31", fields.GetProperty("duedate").GetString());
        Assert.Equal("2026-03-09T14:05:07.123-0330", fields.GetProperty("customfield_90006").GetString());
        Assert.Equal(AccountId, fields.GetProperty("customfield_90007").GetProperty("accountId").GetString());
        Assert.Equal("GCM-5", fields.GetProperty("parent").GetProperty("key").GetString());
        Assert.Equal(
            ["10000", "10001"],
            fields.GetProperty("components").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
        Assert.Equal(["a", "b"], fields.GetProperty("labels").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task GivenAddAndRemoveChanges_WhenUpdating_ThenTheyAreSentUnderUpdateAsOperations()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, GcmIssuePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        await new EditClient(fixture.Transport).UpdateAsync(
            "GCM-1",
            [
                FieldUpdate.ChangeLabels("labels", ["new"], ["old", "older"]),
                FieldUpdate.ChangeOptions("fixVersions", ["7"], ["8"]),
                FieldUpdate.Text("summary", "Mixed"),
            ],
            Cancel());

        using var sent = JsonDocument.Parse(Assert.Single(fixture.Handler.Requests).Body!);
        var root = sent.RootElement;
        Assert.Equal(["summary"], root.GetProperty("fields").EnumerateObject().Select(property => property.Name));
        var labels = root.GetProperty("update").GetProperty("labels").EnumerateArray().ToList();
        Assert.Equal(3, labels.Count);
        Assert.Equal("new", labels[0].GetProperty("add").GetString());
        Assert.Equal("old", labels[1].GetProperty("remove").GetString());
        Assert.Equal("older", labels[2].GetProperty("remove").GetString());
        var versions = root.GetProperty("update").GetProperty("fixVersions").EnumerateArray().ToList();
        Assert.Equal("7", versions[0].GetProperty("add").GetProperty("id").GetString());
        Assert.Equal("8", versions[1].GetProperty("remove").GetProperty("id").GetString());
    }

    [Fact]
    public async Task GivenOnlyOperations_WhenUpdating_ThenNoFieldsSectionIsSent()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, GcmIssuePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        await new EditClient(fixture.Transport).UpdateAsync(
            "GCM-1",
            [FieldUpdate.ChangeLabels("labels", ["x"], [])],
            Cancel());

        using var sent = JsonDocument.Parse(Assert.Single(fixture.Handler.Requests).Body!);
        Assert.False(sent.RootElement.TryGetProperty("fields", out _));
        Assert.True(sent.RootElement.TryGetProperty("update", out _));
    }

    [Fact]
    public async Task GivenNothingDuplicateOrAssignee_WhenUpdating_ThenValidationIsReturnedWithoutARequest()
    {
        using var fixture = new TransportFixture();
        var client = new EditClient(fixture.Transport);

        var empty = await client.UpdateAsync("GCM-1", [], Cancel());
        var twice = await client.UpdateAsync(
            "GCM-1",
            [FieldUpdate.Text("summary", "a"), FieldUpdate.Text("summary", "b")],
            Cancel());
        var assignee = await client.UpdateAsync("GCM-1", [FieldUpdate.User("assignee", AccountId)], Cancel());
        var blank = await client.UpdateAsync("GCM-1", [FieldUpdate.Text(" ", "a")], Cancel());

        Assert.All(
            [empty, twice, assignee, blank],
            result => Assert.Equal(WriteErrorKind.Validation, result.Error.Kind));
        Assert.Contains("assign", assignee.Error.Message);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenFieldNotOnEditScreen_WhenUpdating_ThenValidationNamesTheFieldAndNothingIsRetried()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("adf/edit-400-unknown-field.json");
        fixture.Handler.Serve(HttpMethod.Put, GcmIssuePath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await new EditClient(fixture.Transport)
            .UpdateAsync("GCM-1", [FieldUpdate.Text("nosuchfield", "x")], Cancel());

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.False(result.Error.IsOutcomeUnknown);
        var field = Assert.Single(result.Error.FieldErrors);
        Assert.Equal("nosuchfield", field.FieldId);
        Assert.Contains("cannot be set", field.Message);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenNoEditPermission_WhenUpdating_ThenPermissionErrorCarriesJirasMessage()
    {
        using var fixture = new TransportFixture();
        const string body = """
            {"errorMessages":["You do not have permission to edit issues in this project."],"errors":{}}
            """;
        fixture.Handler.Serve(HttpMethod.Put, GcmIssuePath, FakeResponse.WithStatus(HttpStatusCode.Forbidden, body));

        var result = await new EditClient(fixture.Transport)
            .UpdateAsync("GCM-1", [FieldUpdate.Text("summary", "x")], Cancel());

        Assert.Equal(WriteErrorKind.Permission, result.Error.Kind);
        Assert.False(result.Error.IsOutcomeUnknown);
        Assert.Contains("permission to edit", result.Error.Message);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenLostReply_WhenUpdating_ThenOutcomeIsUnknownAndTheReloadRunsOnce()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, GcmIssuePath, FakeResponse.Broken());
        var client = new EditClient(fixture.Transport);

        var write = await client.UpdateAsync("GCM-1", [FieldUpdate.Text("summary", "x")], Cancel());
        var reloads = 0;
        var reloaded = await ReadAfterWrite.ReloadIfUnknownAsync(
            write,
            token =>
            {
                reloads++;

                return client.GetEditFieldsAsync("GCM-1", token);
            },
            Cancel());

        Assert.True(write.Error.IsOutcomeUnknown);
        Assert.NotNull(reloaded);
        Assert.Equal(1, reloads);
        Assert.Single(fixture.Handler.Requests, request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task GivenRateLimitWithoutRetryAfter_WhenUpdating_ThenWriteIsNotRepeated()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, GcmIssuePath, FakeResponse.WithStatus(HttpStatusCode.TooManyRequests));

        var result = await new EditClient(fixture.Transport)
            .UpdateAsync("GCM-1", [FieldUpdate.Text("summary", "x")], Cancel());

        Assert.Equal(WriteErrorKind.RateLimited, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenAccount_WhenAssigning_ThenBodyCarriesTheAccountId()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, GcmAssigneePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await new EditClient(fixture.Transport)
            .AssignAsync("GCM-1", AssigneeChange.User(AccountId), Cancel());

        Assert.True(result.IsSuccess);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        using var sent = JsonDocument.Parse(request.Body!);
        Assert.Equal(AccountId, sent.RootElement.GetProperty("accountId").GetString());
    }

    [Fact]
    public async Task GivenUnassignAndAutomatic_WhenAssigning_ThenBodiesAreNullAndMinusOne()
    {
        using var unassign = new TransportFixture();
        unassign.Handler.Serve(HttpMethod.Put, GcmAssigneePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));
        using var automatic = new TransportFixture();
        automatic.Handler.Serve(HttpMethod.Put, GcmAssigneePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        await new EditClient(unassign.Transport).AssignAsync("GCM-1", AssigneeChange.Unassign, Cancel());
        await new EditClient(automatic.Transport).AssignAsync("GCM-1", AssigneeChange.Automatic, Cancel());

        using var none = JsonDocument.Parse(Assert.Single(unassign.Handler.Requests).Body!);
        using var auto = JsonDocument.Parse(Assert.Single(automatic.Handler.Requests).Body!);
        Assert.Equal(JsonValueKind.Null, none.RootElement.GetProperty("accountId").ValueKind);
        Assert.Equal("-1", auto.RootElement.GetProperty("accountId").GetString());
        Assert.NotEqual(AssigneeChange.Unassign, AssigneeChange.Automatic);
    }

    [Fact]
    public async Task GivenBlankAccountId_WhenAssigning_ThenValidationWithoutARequest()
    {
        using var fixture = new TransportFixture();

        var result = await new EditClient(fixture.Transport).AssignAsync("GCM-1", AssigneeChange.User(" "), Cancel());

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenAutomaticIdAsAccount_WhenAssigning_ThenValidationWithoutARequest()
    {
        using var fixture = new TransportFixture();

        var result = await new EditClient(fixture.Transport).AssignAsync("GCM-1", AssigneeChange.User("-1"), Cancel());

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void GivenNonFiniteNumber_WhenBuildingUpdate_ThenItThrowsInsteadOfClearing(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FieldUpdate.Number("customfield_90003", value));
    }

    [Fact]
    public async Task GivenUserWhoCannotBeAssigned_WhenAssigning_ThenValidationKeepsJirasMessage()
    {
        using var fixture = new TransportFixture();
        const string body = """{"errorMessages":["User 'x' cannot be assigned issues."],"errors":{}}""";
        fixture.Handler.Serve(
            HttpMethod.Put,
            GcmAssigneePath,
            FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await new EditClient(fixture.Transport).AssignAsync("GCM-1", AssigneeChange.User("x"), Cancel());

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Contains("cannot be assigned", result.Error.Message);
    }

    [Fact]
    public async Task GivenNoAssignPermission_WhenAssigning_ThenPermissionError()
    {
        using var fixture = new TransportFixture();
        const string body = """{"errorMessages":["You do not have permission to assign issues."],"errors":{}}""";
        fixture.Handler.Serve(HttpMethod.Put, GcmAssigneePath, FakeResponse.WithStatus(HttpStatusCode.Forbidden, body));

        var result = await new EditClient(fixture.Transport).AssignAsync("GCM-1", AssigneeChange.Unassign, Cancel());

        Assert.Equal(WriteErrorKind.Permission, result.Error.Kind);
    }

    private static TransportFixture Serving(string path, string fixtureName)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, path, FakeResponse.Ok(FixtureFiles.Read(fixtureName)));

        return fixture;
    }

    private static CancellationToken Cancel() => CancellationToken.None;
}
