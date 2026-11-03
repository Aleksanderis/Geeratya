using System.Text.Json;
using System.Web;
using Geeratya.Jira.Boards;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Tests.Fakes;

namespace Geeratya.Jira.Tests.Search;

public sealed class SprintFieldMappingTests
{
    private static readonly IssueExtraFields _teamFields = new("customfield_10020", ["customfield_10016"]);
    private static readonly IssueExtraFields _companyFields = new("customfield_10020", ["customfield_10047"]);

    [Fact]
    public async Task GivenTeamManagedScrumBoard_WhenLoadingRows_ThenSprintsEstimateAndEpicAreOnTheRows()
    {
        var rows = await RowsAsync(5, "scrum/board-issues-gts.json", _teamFields);

        var noSprint = rows["GTS-22"];
        Assert.Empty(noSprint.Sprints);
        Assert.Null(noSprint.CurrentSprint);
        Assert.Null(noSprint.Estimate);
        Assert.Null(noSprint.Epic);

        var one = rows["GTS-32"];
        Assert.Equal([new IssueSprint(10, "Seed Sprint 2", SprintState.Active)], one.Sprints);
        Assert.Equal(3.0, one.Estimate);
        Assert.Equal("GTS-22", one.Epic?.Key);

        var carried = rows["GTS-29"];
        Assert.Equal([SprintState.Closed, SprintState.Active], carried.Sprints.Select(sprint => sprint.State));
        Assert.Equal(10, carried.CurrentSprint?.Id);
        Assert.Equal(SprintState.Future, rows["GTS-36"].CurrentSprint?.State);
    }

    [Fact]
    public async Task GivenCompanyManagedScrumBoard_WhenLoadingRows_ThenTheBoardsOwnEstimateFieldIsRead()
    {
        var rows = await RowsAsync(6, "scrum/board-issues-gcs.json", _companyFields);

        Assert.Equal(8.0, rows["GCS-8"].Estimate);
        Assert.Equal([6L, 7L], rows["GCS-8"].Sprints.Select(sprint => sprint.Id));
        Assert.Equal("GCS-2", rows["GCS-8"].Epic?.Key);
        Assert.Null(rows["GCS-9"].Estimate);
        Assert.Empty(rows["GCS-18"].Sprints);
    }

    [Fact]
    public async Task GivenSubTask_WhenLoadingRows_ThenItsParentIsNotAnEpic()
    {
        var rows = await RowsAsync(5, "scrum/board-issues-gts.json", _teamFields);

        Assert.Equal("GTS-24", rows["GTS-28"].Parent?.Key);
        Assert.Null(rows["GTS-28"].Epic);
    }

    [Fact]
    public async Task GivenNoExtraFields_WhenLoadingRows_ThenSprintsAndEstimateAreQuietlyAbsent()
    {
        var rows = await RowsAsync(5, "scrum/board-issues-gts.json", IssueExtraFields.None);

        Assert.All(rows.Values, row => Assert.Empty(row.Sprints));
        Assert.All(rows.Values, row => Assert.Null(row.Estimate));
        Assert.Equal("GTS-22", rows["GTS-24"].Epic?.Key);
    }

    [Fact]
    public async Task GivenOddSprintAndEstimateValues_WhenMapping_ThenTheRowIsStillMapped()
    {
        const string json = """
            {"issues":[{"id":"1","key":"X-1","fields":{"summary":"s",
              "customfield_10020":[{"id":7,"name":"A","state":"active"},{"name":"no id"},"text",{"id":"x"},{"id":9}],
              "customfield_10016":"five"}},
             {"id":"2","key":"X-2","fields":{"customfield_10020":"not an array","customfield_10016":null}}],
             "startAt":0,"total":2}
            """;
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, "/rest/agile/1.0/board/5/issue", FakeResponse.Ok(json));
        var query = new BoardIssueQuery { Extras = _teamFields };

        var page = (await new BoardClient(fixture.Transport).GetIssuesAsync(5, query, null, CancellationToken.None)).Value;

        Assert.Equal(["7", "9"], page.Items[0].Sprints.Select(sprint => sprint.Id.ToString()));
        Assert.Equal(SprintState.Unknown, page.Items[0].Sprints[1].State);
        Assert.Null(page.Items[0].Estimate);
        Assert.Empty(page.Items[1].Sprints);
    }

    [Fact]
    public async Task GivenExtraFields_WhenLoadingBoardRows_ThenTheirIdsAreAskedFor()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            "/rest/agile/1.0/board/5/issue",
            FakeResponse.Ok(FixtureFiles.Read("scrum/board-issues-gts.json")));
        var query = new BoardIssueQuery { Extras = _teamFields };

        await new BoardClient(fixture.Transport).GetIssuesAsync(5, query, null, CancellationToken.None);

        var fields = HttpUtility.ParseQueryString(new Uri($"https://x{fixture.Handler.Requests.Single().PathAndQuery}").Query)["fields"];
        Assert.EndsWith(",customfield_10020,customfield_10016", fields, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenExtraFieldsAndLongJql_WhenSearching_ThenThePostBodyAsksForThem()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Post,
            JiraSearchClient.SearchPath,
            FakeResponse.Ok("""{"issues":[],"isLast":true}"""));
        var jql = $"project = X AND summary ~ \"{new string('a', JiraSearchClient.MaxGetPathLength)}\"";
        var query = new IssueSearchQuery(jql) { Extras = _teamFields };

        await new JiraSearchClient(fixture.Transport).SearchAsync(query, null, CancellationToken.None);

        var body = fixture.Handler.Requests.Single().Body;
        Assert.Contains("\"customfield_10020\",\"customfield_10016\"", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("scrum/issue-gts-carried-over.json", "GTS-", 8.0, "customfield_10016")]
    [InlineData("scrum/issue-gcs-carried-over.json", "GCS-", 8.0, "customfield_10047")]
    public async Task GivenCarriedOverIssue_WhenOpening_ThenDetailShowsBothSprintsEstimateAndEpic(
        string fixtureName,
        string prefix,
        double estimate,
        string estimateField)
    {
        var detail = await DetailAsync(fixtureName, new IssueExtraFields("customfield_10020", [estimateField]));

        Assert.StartsWith(prefix, detail.Key, StringComparison.Ordinal);
        Assert.Equal([SprintState.Closed, SprintState.Active], detail.Sprints.Select(sprint => sprint.State));
        Assert.Equal("Seed Sprint 2", detail.CurrentSprint?.Name);
        Assert.Equal(estimate, detail.Estimate);
        Assert.NotNull(detail.Epic);
    }

    [Theory]
    [InlineData("scrum/issue-gcs-no-sprint.json")]
    [InlineData("scrum/issue-gts-no-sprint.json")]
    public async Task GivenIssueWithoutSprint_WhenOpening_ThenDetailHasNoSprintAndNoCrash(string fixtureName)
    {
        var detail = await DetailAsync(fixtureName, new IssueExtraFields("customfield_10020", ["customfield_10016"]));

        Assert.Empty(detail.Sprints);
        Assert.Null(detail.CurrentSprint);
    }

    [Theory]
    [InlineData("scrum/issue-gcs-future-sprint.json")]
    [InlineData("scrum/issue-gts-future-sprint.json")]
    public async Task GivenIssueInFutureSprint_WhenOpening_ThenTheCurrentSprintIsTheFutureOne(string fixtureName)
    {
        var detail = await DetailAsync(fixtureName, new IssueExtraFields("customfield_10020", []));

        Assert.Equal(SprintState.Future, detail.CurrentSprint?.State);
    }

    [Fact]
    public async Task GivenExtraFields_WhenOpeningAnIssue_ThenTheyAreAddedToTheDetailFieldList()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            "/rest/api/3/issue/GCS-8",
            FakeResponse.Ok(FixtureFiles.Read("scrum/issue-gcs-carried-over.json")));

        await new IssueClient(fixture.Transport)
            .GetIssueAsync("GCS-8", CancellationToken.None, extras: _companyFields);

        var path = fixture.Handler.Requests.Single().PathAndQuery;
        Assert.Contains(",customfield_10020,customfield_10047", path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNoExtraFields_WhenOpeningAnIssue_ThenTheFieldListIsTheFixedOne()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            "/rest/api/3/issue/GCS-8",
            FakeResponse.Ok(FixtureFiles.Read("scrum/issue-gcs-carried-over.json")));

        await new IssueClient(fixture.Transport).GetIssueAsync("GCS-8", CancellationToken.None);

        Assert.DoesNotContain("customfield", fixture.Handler.Requests.Single().PathAndQuery, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("scrum/board-gcs-config.json", "customfield_10047")]
    [InlineData("scrum/board-gts-config.json", "customfield_10016")]
    [InlineData("boards/board-gcm-config.json", null)]
    [InlineData("boards/board-gtm-config.json", null)]
    public async Task GivenBoardConfiguration_WhenLoading_ThenTheEstimationFieldIsReadOrAbsent(
        string fixtureName,
        string? expected)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            "/rest/agile/1.0/board/9/configuration",
            FakeResponse.Ok(FixtureFiles.Read(fixtureName)));

        var result = await new BoardClient(fixture.Transport).GetConfigurationAsync(9, CancellationToken.None);

        Assert.Equal(expected, result.Value.EstimationFieldId);
    }

    private static async Task<Dictionary<string, IssueSummary>> RowsAsync(
        long boardId,
        string fixtureName,
        IssueExtraFields extras)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            $"/rest/agile/1.0/board/{boardId}/issue",
            FakeResponse.Ok(FixtureFiles.Read(fixtureName)));
        var query = new BoardIssueQuery { Extras = extras };

        var result = await new BoardClient(fixture.Transport).GetIssuesAsync(boardId, query, null, CancellationToken.None);

        return result.Value.Items.ToDictionary(row => row.Key);
    }

    private static async Task<IssueDetail> DetailAsync(
        string fixtureName,
        IssueExtraFields extras)
    {
        using var fixture = new TransportFixture();
        var json = FixtureFiles.Read(fixtureName);
        var key = JsonDocument.Parse(json).RootElement.GetProperty("key").GetString()!;
        fixture.Handler.Serve(HttpMethod.Get, $"/rest/api/3/issue/{key}", FakeResponse.Ok(json));

        var result = await new IssueClient(fixture.Transport).GetIssueAsync(key, CancellationToken.None, extras: extras);

        return result.Value;
    }
}
