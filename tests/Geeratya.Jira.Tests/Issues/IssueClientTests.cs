using System.Net;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Issues;

public sealed class IssueClientTests
{
    [Fact]
    public async Task GivenTeamManagedStoryWithEverything_WhenGetting_ThenParentSubtasksLinksAttachmentsAreMapped()
    {
        using var fixture = Serving("GTM-11", "issues/issue-gtm11.json");

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("GTM-11", CancellationToken.None);

        var issue = result.Value;
        Assert.Equal("GTM-11", issue.Key);
        Assert.Equal("Sign up with email", issue.Summary);
        Assert.Equal("GTM", issue.ProjectKey);
        Assert.Equal("Story", issue.IssueType.Name);
        Assert.Equal("Test User 1", issue.Reporter?.DisplayName);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 17, 48, 55, 711, TimeSpan.FromHours(2)), issue.Created);
        Assert.NotNull(issue.Description);
        Assert.Equal("GTM-9", issue.Parent?.Key);
        Assert.Equal("Seed: Onboarding flow", issue.Parent?.Summary);
        Assert.Equal(1, issue.Parent?.IssueType.HierarchyLevel);
        Assert.Equal("indeterminate", issue.Parent?.Status.CategoryKey);
        Assert.Equal(["GTM-19", "GTM-20"], issue.Subtasks.Select(stub => stub.Key));
        Assert.True(issue.Subtasks[0].IssueType.IsSubtask);
        Assert.Equal("done", issue.Subtasks[0].Status.CategoryKey);
        var attachment = Assert.Single(issue.Attachments);
        Assert.Equal("seed-notes.txt", attachment.FileName);
        Assert.Equal("text/plain", attachment.MimeType);
        Assert.Equal(47, attachment.Size);
        Assert.Equal("Test User 1", attachment.Author?.DisplayName);
        Assert.Equal("https://example.atlassian.net/rest/api/3/attachment/content/10003", attachment.ContentUrl);
    }

    [Fact]
    public async Task GivenLinksInBothDirections_WhenGetting_ThenDirectionTextFollowsTheSidePresent()
    {
        using var fixture = Serving("GTM-11", "issues/issue-gtm11.json");

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("GTM-11", CancellationToken.None);

        var links = result.Value.Links;
        Assert.Equal(2, links.Count);
        Assert.Equal(IssueLinkDirection.Inward, links[0].Direction);
        Assert.Equal("Blocks", links[0].TypeName);
        Assert.Equal("is blocked by", links[0].DirectionText);
        Assert.Equal("GTM-12", links[0].Issue.Key);
        Assert.Equal("Welcome screen", links[0].Issue.Summary);
        Assert.Equal(IssueLinkDirection.Outward, links[1].Direction);
        Assert.Equal("relates to", links[1].DirectionText);
        Assert.Equal("GTM-17", links[1].Issue.Key);
    }

    [Fact]
    public async Task GivenOutwardOnlyLink_WhenGetting_ThenOutwardTextIsUsed()
    {
        const string json = """
            {"id": "1", "key": "A-1", "fields": {"issuelinks": [
              {"id": "9", "type": {"name": "Blocks", "inward": "is blocked by", "outward": "blocks"},
               "outwardIssue": {"key": "A-2", "fields": {"summary": "Second"}}},
              {"id": "10", "type": {"name": "Blocks"}}]}}
            """;
        using var fixture = Serving("A-1", json, true);

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("A-1", CancellationToken.None);

        var link = Assert.Single(result.Value.Links);
        Assert.Equal(IssueLinkDirection.Outward, link.Direction);
        Assert.Equal("blocks", link.DirectionText);
        Assert.Equal("A-2", link.Issue.Key);
    }

    [Fact]
    public async Task GivenCompanyManagedSubtask_WhenGetting_ThenParentIsMappedAndDescriptionIsNull()
    {
        using var fixture = Serving("GCM-6", "issues/issue-gcm6.json");

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("GCM-6", CancellationToken.None);

        var issue = result.Value;
        Assert.Equal("Sub-task", issue.IssueType.Name);
        Assert.True(issue.IssueType.IsSubtask);
        Assert.Equal("GCM-1", issue.Parent?.Key);
        Assert.Equal("Survey task one", issue.Parent?.Summary);
        Assert.Null(issue.Description);
        Assert.Empty(issue.Subtasks);
        Assert.Empty(issue.Links);
        Assert.Empty(issue.Attachments);
    }

    [Fact]
    public async Task GivenCompanyManagedStoryWithEverything_WhenGetting_ThenRelationsAreMapped()
    {
        using var fixture = Serving("GCM-10", "issues/issue-gcm10.json");

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("GCM-10", CancellationToken.None);

        var issue = result.Value;
        Assert.Equal("GCM-8", issue.Parent?.Key);
        Assert.Equal(["GCM-18", "GCM-19"], issue.Subtasks.Select(stub => stub.Key));
        Assert.Equal(["is blocked by", "relates to"], issue.Links.Select(link => link.DirectionText));
        Assert.Equal("seed-notes.txt", Assert.Single(issue.Attachments).FileName);
        Assert.NotNull(issue.Description);
    }

    [Fact]
    public async Task GivenEpicWithoutParent_WhenGetting_ThenParentIsNull()
    {
        using var fixture = Serving("GTM-9", "issues/issue-gtm9.json");

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("GTM-9", CancellationToken.None);

        Assert.Null(result.Value.Parent);
        Assert.Equal("Epic", result.Value.IssueType.Name);
    }

    [Fact]
    public async Task GivenNullDescription_WhenGetting_ThenDescriptionIsNull()
    {
        using var fixture = Serving("GCM-2", "issues/issue-gcm2.json");

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("GCM-2", CancellationToken.None);

        Assert.Null(result.Value.Description);
        Assert.Equal("Survey bug two", result.Value.Summary);
    }

    [Fact]
    public async Task GivenMediaDescriptionAndImageAttachment_WhenGetting_ThenAdfIsParsedAndAttachmentIsMetadataOnly()
    {
        using var fixture = Serving("GCM-24", "issues/issue-gcm24.json");

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("GCM-24", CancellationToken.None);

        var issue = result.Value;
        Assert.Equal(2, issue.Description?.Content.Count);
        var attachment = Assert.Single(issue.Attachments);
        Assert.Equal("pixel.png", attachment.FileName);
        Assert.Equal("image/png", attachment.MimeType);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenEmptyDocumentAndUnknownFields_WhenGetting_ThenDescriptionIsNullAndNothingThrows()
    {
        const string json = """
            {"id": "1", "key": "A-1", "extra": [1], "fields": {"description": {"type": "doc", "version": 1,
             "content": []}, "futureField": {"a": 1}, "summary": "S", "labels": null, "subtasks": null,
             "attachment": [{"id": "5", "filename": "räpörtti 日本語 🎉.pdf", "mimeType": "application/pdf",
             "size": 2048, "created": "garbage"}]}}
            """;
        using var fixture = Serving("A-1", json, true);

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("A-1", CancellationToken.None);

        var issue = result.Value;
        Assert.Null(issue.Description);
        Assert.Empty(issue.Labels);
        Assert.Empty(issue.Subtasks);
        var attachment = Assert.Single(issue.Attachments);
        Assert.Equal("räpörtti 日本語 🎉.pdf", attachment.FileName);
        Assert.Null(attachment.Created);
        Assert.Null(attachment.Author);
        Assert.Null(attachment.ContentUrl);
    }

    [Fact]
    public async Task GivenNullEntriesInRelationLists_WhenGetting_ThenTheyAreSkipped()
    {
        const string json = """
            {"id": "1", "key": "A-1", "fields": {"attachment": [null], "subtasks": [null],
             "issuelinks": [null]}}
            """;
        using var fixture = Serving("A-1", json, true);

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("A-1", CancellationToken.None);

        Assert.Empty(result.Value.Attachments);
        Assert.Empty(result.Value.Subtasks);
        Assert.Empty(result.Value.Links);
    }

    [Fact]
    public async Task GivenKey_WhenGetting_ThenRequestAsksForTheDetailFieldsOnly()
    {
        using var fixture = Serving("GTM-9", "issues/issue-gtm9.json");

        await new IssueClient(fixture.Transport).GetIssueAsync("GTM-9", CancellationToken.None);

        var request = Assert.Single(fixture.Handler.Requests);
        Assert.StartsWith("/rest/api/3/issue/GTM-9?fields=summary,status,", request.PathAndQuery);
        Assert.Contains("description", request.PathAndQuery);
        Assert.Contains("issuelinks,attachment", request.PathAndQuery);
        Assert.DoesNotContain("*", request.PathAndQuery);
    }

    [Fact]
    public async Task GivenMissingIssue_WhenGetting_ThenNotFoundSaysNoAccess()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/issue-404.json");
        var reply = FakeResponse.WithStatus(HttpStatusCode.NotFound, body);
        fixture.Handler.Serve(HttpMethod.Get, "/rest/api/3/issue/GTM-999", reply);

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("GTM-999", CancellationToken.None);

        Assert.Equal(JiraErrorKind.NotFound, result.Error.Kind);
        Assert.Equal("Not found or no access.", result.Error.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, JiraErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, JiraErrorKind.Forbidden)]
    public async Task GivenErrorReply_WhenGetting_ThenTypedErrorIsReturned(HttpStatusCode status, JiraErrorKind kind)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, "/rest/api/3/issue/GTM-1", FakeResponse.WithStatus(status));

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("GTM-1", CancellationToken.None);

        Assert.Equal(kind, result.Error.Kind);
    }

    [Fact]
    public async Task GivenRateLimitThenOk_WhenGetting_ThenRequestIsRetriedAfterTheWait()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Get,
            "/rest/api/3/issue/GTM-9",
            FakeResponse.RateLimited("2"),
            FakeResponse.Ok(FixtureFiles.Read("issues/issue-gtm9.json")));

        var result = await new IssueClient(fixture.Transport).GetIssueAsync("GTM-9", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Contains(TimeSpan.FromSeconds(2), fixture.Waits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("gtm-1")]
    [InlineData("GTM-1/../../x")]
    [InlineData("GTM-1?expand=x")]
    public async Task GivenInvalidKey_WhenGetting_ThenNotFoundWithoutARequest(string key)
    {
        using var fixture = new TransportFixture();

        var result = await new IssueClient(fixture.Transport).GetIssueAsync(key, CancellationToken.None);

        Assert.Equal(JiraErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    private static TransportFixture Serving(string key, string fixtureOrJson, bool isJson = false)
    {
        var body = isJson ? fixtureOrJson : FixtureFiles.Read(fixtureOrJson);
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, $"/rest/api/3/issue/{key}", FakeResponse.Ok(body));

        return fixture;
    }
}
