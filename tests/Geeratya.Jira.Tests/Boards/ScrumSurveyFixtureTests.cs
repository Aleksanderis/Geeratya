using System.Text.Json;
using System.Text.RegularExpressions;
using Geeratya.Jira.Boards;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Tests.Fakes;

namespace Geeratya.Jira.Tests.Boards;

/// <summary>
/// Guards the Scrum survey (#172, <c>docs/jira-layer.md</c>, "Scrum survey"): the recorded fixtures under
/// <c>Fixtures/scrum</c> must stay scrubbed, and the existing board client must read the two Scrum boards.
/// </summary>
public sealed partial class ScrumSurveyFixtureTests
{
    private static readonly string[] _futureSprintKeys = ["GCS-15", "GCS-16", "GCS-17"];

    [Fact]
    public void GivenScrumFixtures_WhenScanned_ThenEachParsesAndCarriesNoRealHostOrAccountData()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "scrum");
        var files = Directory.GetFiles(directory, "*.json");

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            using var document = JsonDocument.Parse(text);

            foreach (Match match in HostPattern().Matches(text))
            {
                Assert.Equal("example.atlassian.net", match.Value);
            }

            Assert.DoesNotContain("\"accountId\"", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\"emailAddress\"", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GivenCompanyManagedScrumConfiguration_WhenLoading_ThenTypeIsScrumAndNoBacklogColumnExists()
    {
        using var fixture = Serving("/rest/agile/1.0/board/6/configuration", "scrum/board-gcs-config.json");

        var result = await new BoardClient(fixture.Transport).GetConfigurationAsync(6, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BoardType.Scrum, result.Value.Type);
        Assert.Equal(["To Do", "In Progress", "Done"], result.Value.Columns.Select(c => c.Name));
        Assert.Equal("customfield_10019", result.Value.RankFieldId);
    }

    [Fact]
    public async Task GivenTeamManagedScrumConfiguration_WhenLoading_ThenTypeIsSimpleLikeTeamManagedKanban()
    {
        using var fixture = Serving("/rest/agile/1.0/board/5/configuration", "scrum/board-gts-config.json");

        var result = await new BoardClient(fixture.Transport).GetConfigurationAsync(5, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BoardType.Simple, result.Value.Type);
        Assert.Equal(["To Do", "In Progress", "Done"], result.Value.Columns.Select(c => c.Name));
    }

    [Fact]
    public async Task GivenScrumBoardFixture_WhenGettingBoard_ThenSummaryIsScrum()
    {
        using var fixture = Serving("/rest/agile/1.0/board/6", "scrum/board-gcs.json");

        var result = await new BoardClient(fixture.Transport).GetBoardAsync(6, CancellationToken.None);

        Assert.Equal(new BoardSummary(6, "GCS board", BoardType.Scrum, "GCS"), result.Value);
    }

    [Fact]
    public async Task GivenCompanyManagedScrumBoard_WhenLoadingBoardAndBacklog_ThenBacklogLeavesOutSprintIssues()
    {
        using var boardFixture = Serving("/rest/agile/1.0/board/6/issue", "scrum/board-issues-gcs.json");
        using var backlogFixture = Serving("/rest/agile/1.0/board/6/backlog", "scrum/backlog-gcs.json");
        var query = new BoardIssueQuery();

        var board = await new BoardClient(boardFixture.Transport).GetIssuesAsync(6, query, null, CancellationToken.None);
        var backlog = await new BoardClient(backlogFixture.Transport)
            .GetBacklogAsync(6, query, null, CancellationToken.None);

        Assert.Equal(19, board.Value.Items.Count);
        Assert.Equal(["GCS-18", "GCS-19", "GCS-20", "GCS-21"], backlog.Value.Items.Select(i => i.Key));
        Assert.All(_futureSprintKeys, key => Assert.Contains(board.Value.Items, i => i.Key == key));
        Assert.All(_futureSprintKeys, key => Assert.DoesNotContain(backlog.Value.Items, i => i.Key == key));
    }

    [Fact]
    public async Task GivenTeamManagedScrumBoard_WhenLoadingBoard_ThenEpicsAreRows()
    {
        using var fixture = Serving("/rest/agile/1.0/board/5/issue", "scrum/board-issues-gts.json");

        var result = await new BoardClient(fixture.Transport)
            .GetIssuesAsync(5, new BoardIssueQuery(), null, CancellationToken.None);

        Assert.Equal(21, result.Value.Items.Count);
        Assert.Contains(result.Value.Items, i => i.Key == "GTS-22" && i.IssueType.Name == "Epic");
    }

    [GeneratedRegex(@"[a-z0-9-]+\.atlassian\.net", RegexOptions.IgnoreCase)]
    private static partial Regex HostPattern();

    private static TransportFixture Serving(string path, string fixtureName)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, path, FakeResponse.Ok(FixtureFiles.Read(fixtureName)));

        return fixture;
    }
}
