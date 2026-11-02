using System.Net;
using Geeratya.Jira.Edit;
using Geeratya.Jira.Jql;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Edit;

public sealed class EditClientDeleteTests
{
    private const string IssuePath = "/rest/api/3/issue/GTM-6";
    private const string WithSubtasksPath = "/rest/api/3/issue/GTM-6?deleteSubtasks=true";
    private const string WithoutSubtasksPath = "/rest/api/3/issue/GTM-6?deleteSubtasks=false";

    [Theory]
    [InlineData(true, WithSubtasksPath)]
    [InlineData(false, WithoutSubtasksPath)]
    public async Task GivenDeleteSubtasksFlag_WhenDeleting_ThenOneDeleteRequestCarriesItInTheQuery(
        bool deleteSubtasks,
        string path)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Delete, IssuePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await new EditClient(fixture.Transport)
            .DeleteIssueAsync("GTM-6", deleteSubtasks, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal(path, request.PathAndQuery);
        Assert.Null(request.Body);
    }

    [Fact]
    public async Task GivenRecordedHasSubtasksReply_WhenDeletingWithoutTheFlag_ThenValidationNamesTheSubtasks()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("hierarchy/delete-400-has-subtasks.json");
        var reply = FakeResponse.WithStatus(HttpStatusCode.BadRequest, body);
        fixture.Handler.Serve(HttpMethod.Delete, IssuePath, reply);

        var result = await new EditClient(fixture.Transport).DeleteIssueAsync("GTM-6", false, CancellationToken.None);

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Contains("has subtasks", result.Error.Message);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenRecordedNoPermissionReply_WhenDeleting_ThenPermissionWithJirasReason()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/issue-delete-403-no-permission.json");
        var reply = FakeResponse.WithStatus(HttpStatusCode.Forbidden, body);
        fixture.Handler.Serve(HttpMethod.Delete, IssuePath, reply);

        var result = await new EditClient(fixture.Transport).DeleteIssueAsync("GTM-6", false, CancellationToken.None);

        Assert.Equal(WriteErrorKind.Permission, result.Error.Kind);
        Assert.Contains("permission to delete issues", result.Error.Message);
    }

    [Theory]
    [InlineData("not a key")]
    [InlineData("../GTM-6")]
    [InlineData("")]
    public async Task GivenInvalidKey_WhenDeleting_ThenItIsRefusedWithoutARequest(string key)
    {
        using var fixture = new TransportFixture();

        var result = await new EditClient(fixture.Transport).DeleteIssueAsync(key, true, CancellationToken.None);

        Assert.Equal(WriteErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public void GivenAnIssueKey_WhenBuildingTheSubtaskQuery_ThenItSelectsOnlySubtaskTypes()
    {
        Assert.Equal("parent = GTM-6 AND issuetype in subTaskIssueTypes()", JqlBuilder.SubTasksOf("GTM-6"));
    }
}
