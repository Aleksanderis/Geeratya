using Geeratya.Jira.Suggestions;
using Geeratya.Jira.Tests.Fakes;

namespace Geeratya.Jira.Tests.Suggestions;

/// <summary>Suggestion sources against replies recorded from the dev site (GCM company-managed, GTM team-managed).</summary>
public sealed class RecordedSuggestionTests
{
    private const string Assignable = "/rest/api/3/user/assignable/search";
    private const string PickerPath = "/rest/api/3/issue/picker";

    [Theory]
    [InlineData("GCM", "suggestions/recorded-users-assignable-gcm.json")]
    [InlineData("GTM", "suggestions/recorded-users-assignable-gtm.json")]
    public async Task GivenRecordedAssignableUsers_WhenSearching_ThenUsersAreMapped(string project, string file)
    {
        using var fixture = Serving(Assignable, file);

        var result = await new SuggestionClient(fixture.Transport)
            .SearchAssignableUsersForProjectAsync(project, "a", CancellationToken.None);

        var user = Assert.Single(result.Value);
        Assert.Equal(("fake-account-1", "Test User 1", false), (user.Id, user.Label, user.IsInactive));
    }

    [Fact]
    public async Task GivenRecordedSiteLabels_WhenSearching_ThenProjectLabelsAreFound()
    {
        using var fixture = Serving("/rest/api/3/label", "suggestions/recorded-labels.json");

        var result = await new SuggestionClient(fixture.Transport)
            .SearchLabelsAsync("geeratya-seed-GTM-bug", [], CancellationToken.None);

        Assert.Equal(
            ["geeratya-seed-GTM-bug-crash", "geeratya-seed-GTM-bug-date"],
            result.Value.Select(label => label.Label));
    }

    [Theory]
    [InlineData("GCM")]
    [InlineData("GTM")]
    public async Task GivenProjectWithoutComponentsOrVersions_WhenSearching_ThenListsAreEmptyNotErrors(string project)
    {
        using var fixture = new TransportFixture();
        var key = project.ToLowerInvariant();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"/rest/api/3/project/{project}/components",
            FakeResponse.Ok(FixtureFiles.Read($"suggestions/recorded-components-{key}.json")));
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"/rest/api/3/project/{project}/versions",
            FakeResponse.Ok(FixtureFiles.Read($"suggestions/recorded-versions-{key}.json")));
        var client = new SuggestionClient(fixture.Transport);

        var components = await client.SearchComponentsAsync(project, "a", CancellationToken.None);
        var versions = await client.SearchVersionsAsync(project, "1", CancellationToken.None);

        Assert.True(components.IsSuccess);
        Assert.Empty(components.Value);
        Assert.True(versions.IsSuccess);
        Assert.Empty(versions.Value);
    }

    [Theory]
    [InlineData("GCM", "suggestions/recorded-issue-picker.json")]
    [InlineData("GTM", "suggestions/recorded-issue-picker-gtm.json")]
    public async Task GivenRecordedPicker_WhenSearchingIssues_ThenEmptyHistorySectionIsIgnoredAndKeysMapped(
        string project,
        string file)
    {
        using var fixture = Serving(PickerPath, file);

        var result = await new SuggestionClient(fixture.Transport)
            .SearchIssuesAsync($"{project}-1", null, CancellationToken.None);

        Assert.NotEmpty(result.Value);
        Assert.All(result.Value, issue => Assert.StartsWith($"{project}-", issue.Id));
        Assert.All(result.Value, issue => Assert.False(string.IsNullOrWhiteSpace(issue.Label)));
        Assert.Equal(result.Value.Count, result.Value.Select(issue => issue.Id).Distinct().Count());
    }

    private static TransportFixture Serving(string path, string fixtureFile)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, path, FakeResponse.Ok(FixtureFiles.Read(fixtureFile)));

        return fixture;
    }
}
