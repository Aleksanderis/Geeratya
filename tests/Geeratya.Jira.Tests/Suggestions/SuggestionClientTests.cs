using System.Net;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Suggestions;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Suggestions;

public sealed class SuggestionClientTests
{
    private const string Assignable = "/rest/api/3/user/assignable/search";
    private const string LabelPath = "/rest/api/3/label";
    private const string PickerPath = "/rest/api/3/issue/picker";

    [Fact]
    public async Task GivenAssignableUsers_WhenSearching_ThenInactiveAndOddEntriesAreHandled()
    {
        using var fixture = Serving(Assignable, "suggestions/users-assignable-gcm.json");

        var result = await new SuggestionClient(fixture.Transport)
            .SearchAssignableUsersAsync("GCM-6", " te ", CancellationToken.None);

        Assert.Collection(
            result.Value,
            user => Assert.Equal(("fake-account-1", "Test User 1", false), (user.Id, user.Label, user.IsInactive)),
            user => Assert.Equal(("Former user", true), (user.Label, user.IsInactive)),
            user => Assert.Equal("Żółć 日本語 🚀", user.Label));
        Assert.Equal(
            $"{Assignable}?issueKey=GCM-6&query=te&maxResults=20",
            Assert.Single(fixture.Handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task GivenProject_WhenSearchingAssignableForCreate_ThenProjectScopeIsSent()
    {
        using var fixture = Serving(Assignable, "suggestions/users-none.json");

        var result = await new SuggestionClient(fixture.Transport)
            .SearchAssignableUsersForProjectAsync("GTM", "a b", CancellationToken.None);

        Assert.Empty(result.Value);
        Assert.Equal(
            $"{Assignable}?project=GTM&query=a%20b&maxResults=20",
            Assert.Single(fixture.Handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task GivenMentionQuery_WhenSearching_ThenViewIssueEndpointIsUsed()
    {
        using var fixture = Serving("/rest/api/3/user/viewissue/search", "suggestions/users-assignable-gcm.json");

        var result = await new SuggestionClient(fixture.Transport)
            .SearchMentionableUsersAsync("GTM-5", "x", CancellationToken.None);

        Assert.Equal(3, result.Value.Count);
        Assert.Equal(
            "/rest/api/3/user/viewissue/search?issueKey=GTM-5&query=x&maxResults=20",
            Assert.Single(fixture.Handler.Requests).PathAndQuery);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GivenEmptyQuery_WhenSearchingAnySource_ThenNothingIsReturnedWithoutRequest(string? query)
    {
        using var fixture = new TransportFixture();
        var client = new SuggestionClient(fixture.Transport);

        Assert.Empty((await client.SearchAssignableUsersAsync("GCM-1", query, CancellationToken.None)).Value);
        Assert.Empty((await client.SearchMentionableUsersAsync("GCM-1", query, CancellationToken.None)).Value);
        Assert.Empty((await client.SearchLabelsAsync(query, ["a"], CancellationToken.None)).Value);
        Assert.Empty((await client.SearchComponentsAsync("GCM", query, CancellationToken.None)).Value);
        Assert.Empty((await client.SearchVersionsAsync("GCM", query, CancellationToken.None)).Value);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Theory]
    [InlineData("gcm-1")]
    [InlineData("GCM-1&x=1")]
    [InlineData("")]
    public async Task GivenInvalidIssueKey_WhenSearchingUsers_ThenNotFoundWithoutRequest(string key)
    {
        using var fixture = new TransportFixture();

        var result = await new SuggestionClient(fixture.Transport)
            .SearchAssignableUsersAsync(key, "a", CancellationToken.None);

        Assert.Equal(JiraErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenInvalidProjectKey_WhenSearchingComponents_ThenNotFoundWithoutRequest()
    {
        using var fixture = new TransportFixture();

        var result = await new SuggestionClient(fixture.Transport)
            .SearchComponentsAsync("../x", "a", CancellationToken.None);

        Assert.Equal(JiraErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenTwoLabelPages_WhenSearching_ThenKnownAndSiteLabelsAreFilteredPrefixFirst()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Get,
            LabelPath,
            FakeResponse.Ok(FixtureFiles.Read("suggestions/labels-page1.json")),
            FakeResponse.Ok(FixtureFiles.Read("suggestions/labels-page2-last.json")));

        var result = await new SuggestionClient(fixture.Transport)
            .SearchLabelsAsync("front", ["my-frontend", "FRONTEND-old"], CancellationToken.None);

        Assert.Equal(
            ["FRONTEND-old", "Frontend", "front-door", "my-frontend"],
            result.Value.Select(label => label.Label));
        Assert.Equal(
            [$"{LabelPath}?startAt=0&maxResults=1000", $"{LabelPath}?startAt=2&maxResults=1000"],
            fixture.Handler.Requests.Select(request => request.PathAndQuery));
    }

    [Fact]
    public async Task GivenLabelCallFails_WhenLocalLabelsMatch_ThenLocalLabelsAreReturned()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, LabelPath, FakeResponse.WithStatus(HttpStatusCode.Forbidden));
        var client = new SuggestionClient(fixture.Transport);

        var withLocal = await client.SearchLabelsAsync("ba", ["bar"], CancellationToken.None);
        var without = await client.SearchLabelsAsync("zz", ["bar"], CancellationToken.None);

        Assert.Equal("bar", Assert.Single(withLocal.Value).Label);
        Assert.Equal(JiraErrorKind.Forbidden, without.Error.Kind);
    }

    [Fact]
    public async Task GivenEndlessLabelPages_WhenSearching_ThenWalkStopsAtPageCap()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            LabelPath,
            FakeResponse.Ok("""{"isLast": false, "values": ["a"]}"""));

        var result = await new SuggestionClient(fixture.Transport).SearchLabelsAsync("a", [], CancellationToken.None);

        Assert.Single(result.Value);
        Assert.Equal(SuggestionClient.MaxLabelPages, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenPickerReply_WhenSearchingIssues_ThenKeysAreDeduplicatedAndSummariesPlain()
    {
        using var fixture = Serving(PickerPath, "suggestions/issue-picker.json");

        var result = await new SuggestionClient(fixture.Transport)
            .SearchIssuesAsync("fix", "project = GCM AND status = \"To Do\"", CancellationToken.None);

        Assert.Collection(
            result.Value,
            issue => Assert.Equal(("GCM-6", "Fix login", "GCM-6"), (issue.Id, issue.Label, issue.Detail)),
            issue => Assert.Equal(("GCM-7", "Fix & <logout>"), (issue.Id, issue.Label)));
        Assert.Equal(
            $"{PickerPath}?query=fix&currentJQL=project%20%3D%20GCM%20AND%20status%20%3D%20%22To%20Do%22",
            Assert.Single(fixture.Handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task GivenEmptyQuery_WhenSearchingIssues_ThenPickerIsAskedForRecents()
    {
        using var fixture = Serving(PickerPath, "suggestions/issue-picker-empty.json");

        var result = await new SuggestionClient(fixture.Transport)
            .SearchIssuesAsync(null, null, CancellationToken.None);

        Assert.Empty(result.Value);
        Assert.Equal($"{PickerPath}?query=", Assert.Single(fixture.Handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task GivenCompanyManagedProject_WhenSearchingComponents_ThenNamesAreFilteredAndIdlessSkipped()
    {
        using var fixture = Serving("/rest/api/3/project/GCM/components", "suggestions/components-gcm.json");

        var result = await new SuggestionClient(fixture.Transport)
            .SearchComponentsAsync("GCM", "END", CancellationToken.None);

        Assert.Equal(["Backend", "Frontend"], result.Value.Select(component => component.Label));
    }

    [Fact]
    public async Task GivenTeamManagedProject_WhenSearchingComponents_ThenListIsEmptyNotAnError()
    {
        using var fixture = Serving("/rest/api/3/project/GTM/components", "suggestions/components-gtm.json");

        var result = await new SuggestionClient(fixture.Transport)
            .SearchComponentsAsync("GTM", "a", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GivenVersions_WhenSearching_ThenArchivedIsMarkedAndPrefixRanksFirst()
    {
        using var fixture = Serving("/rest/api/3/project/GCM/versions", "suggestions/versions-gcm.json");
        var client = new SuggestionClient(fixture.Transport);

        var both = await client.SearchVersionsAsync("GCM", "2", CancellationToken.None);
        var archived = await client.SearchVersionsAsync("GCM", "1.0", CancellationToken.None);

        Assert.Equal(["2.0", "v2.1 beta"], both.Value.Select(version => version.Label));
        Assert.True(Assert.Single(archived.Value).IsInactive);
    }

    [Fact]
    public void GivenAllowedValues_WhenSearching_ThenNoRequestIsNeededAndMatchesAreFiltered()
    {
        var field = new CreateField(
            "priority",
            "Priority",
            false,
            false,
            CreateFieldKind.SingleChoice,
            [new CreateAllowedValue("1", "Highest"), new CreateAllowedValue("5", "Lowest")],
            null);

        var result = SuggestionClient.SearchAllowedValues(field, "high");

        Assert.Equal("1", Assert.Single(result).Id);
    }

    [Fact]
    public async Task GivenOwnHostAutoCompleteUrl_WhenSearching_ThenQueryIsAppendedAndSuggestionsMapped()
    {
        using var fixture = Serving("/rest/api/1.0/labels/suggest", "suggestions/autocomplete-labels.json");
        var url = $"https://{TransportFixture.Host}/rest/api/1.0/labels/suggest?query=";

        var result = await new SuggestionClient(fixture.Transport)
            .SearchByAutoCompleteUrlAsync(url, "a&b", CancellationToken.None);

        Assert.Equal(["alpha", "Beta"], result.Value.Select(entry => entry.Label));
        Assert.Equal("beta", result.Value[1].Id);
        Assert.Equal(
            "/rest/api/1.0/labels/suggest?query=a%26b",
            Assert.Single(fixture.Handler.Requests).PathAndQuery);
    }

    [Theory]
    [InlineData("https://evil.example.com/rest/api/1.0/labels/suggest?query=")]
    [InlineData("https://example.atlassian.net.evil.com/rest/api/2/x?q=")]
    [InlineData("http://example.atlassian.net/rest/api/2/x?q=")]
    [InlineData("https://user@example.atlassian.net/rest/api/2/x?q=")]
    [InlineData("https://example.atlassian.net/other/path?q=")]
    [InlineData("/rest/api/2/x?q=")]
    public async Task GivenForeignOrOddAutoCompleteUrl_WhenSearching_ThenRefusedWithoutRequest(string url)
    {
        using var fixture = new TransportFixture();

        var result = await new SuggestionClient(fixture.Transport)
            .SearchByAutoCompleteUrlAsync(url, "a", CancellationToken.None);

        Assert.Equal(JiraErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenRateLimitThenSuccess_WhenSearchingUsers_ThenRetryAfterIsHonoured()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Get,
            Assignable,
            FakeResponse.RateLimited("2"),
            FakeResponse.Ok(FixtureFiles.Read("suggestions/users-assignable-gcm.json")));

        var result = await new SuggestionClient(fixture.Transport)
            .SearchAssignableUsersAsync("GCM-6", "t", CancellationToken.None);

        Assert.Equal(3, result.Value.Count);
        Assert.Contains(fixture.Waits, wait => wait >= TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GivenPersistentRateLimit_WhenSearchingUsers_ThenRateLimitedErrorIsReturned()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, Assignable, FakeResponse.RateLimited("1"));

        var result = await new SuggestionClient(fixture.Transport)
            .SearchAssignableUsersAsync("GCM-6", "t", CancellationToken.None);

        Assert.Equal(JiraErrorKind.RateLimited, result.Error.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, JiraErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, JiraErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized, JiraErrorKind.Unauthorized)]
    public async Task GivenErrorStatus_WhenSearchingIssues_ThenTypedErrorIsReturned(
        HttpStatusCode status,
        JiraErrorKind kind)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, PickerPath, FakeResponse.WithStatus(status));

        var result = await new SuggestionClient(fixture.Transport).SearchIssuesAsync("a", null, CancellationToken.None);

        Assert.Equal(kind, result.Error.Kind);
    }

    [Fact]
    public async Task GivenHangingServer_WhenCallerCancels_ThenOperationCanceledIsThrown()
    {
        using var fixture = new TransportFixture();
        using var source = new CancellationTokenSource();
        fixture.Handler.Serve(HttpMethod.Get, Assignable, FakeResponse.Hanging());

        var pending = new SuggestionClient(fixture.Transport).SearchAssignableUsersAsync("GCM-6", "a", source.Token);
        await fixture.Handler.WaitForInFlightAsync(1);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    private static TransportFixture Serving(string path, string fixtureFile)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, path, FakeResponse.Ok(FixtureFiles.Read(fixtureFile)));

        return fixture;
    }
}
