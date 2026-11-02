using System.Globalization;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ViewBodyRendererTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);

    [Fact]
    public void GivenNoTab_WhenBuilding_ThenTheStartScreenStillShows()
    {
        var lines = FrameBuilder.Build(AppState.Initial, 60, 10, _helpLines, _keymap);

        Assert.Contains(lines, line => line.Contains("Geeratya"));
    }

    [Fact]
    public void GivenLoadingTab_WhenBuilding_ThenTitleAndLoadingShow()
    {
        var lines = Frame(Tab(LoadState.Idle) with { Load = new Loading(LoadReason.Open) });

        Assert.StartsWith("project = A  (0 issues)", lines[0]);
        Assert.StartsWith("Loading...", lines[1]);
    }

    [Fact]
    public void GivenLoadingMore_WhenBuilding_ThenTheStateLineSaysSo()
    {
        var lines = Frame(Tab(LoadState.Idle, 3) with { Load = new Loading(LoadReason.NextPage) });

        Assert.StartsWith("Loading more...", lines[1]);
    }

    [Fact]
    public void GivenFailedTab_WhenBuilding_ThenTheMessageShowsAboveTheKeptRows()
    {
        var lines = Frame(Tab(new Failed(JiraErrorKind.Unauthorized, "Jira rejected the credentials."), 2));

        Assert.StartsWith("Jira rejected the credentials.", lines[1]);
        Assert.Contains("A-1", lines[2]);
    }

    [Fact]
    public void GivenEmptyIdleTab_WhenBuilding_ThenNoIssuesShows()
    {
        Assert.StartsWith("No issues.", Frame(Tab(LoadState.Idle))[1]);
    }

    [Fact]
    public void GivenTruncatedTab_WhenBuilding_ThenTheCapIsNamed()
    {
        var lines = Frame(Tab(LoadState.Idle, 2) with { Truncated = true });

        Assert.StartsWith("Load more stopped at 10 000 rows.", lines[1]);
    }

    [Fact]
    public void GivenSelection_WhenBuilding_ThenOnlyTheSelectedRowHasTheMarker()
    {
        var lines = Frame(Tab(LoadState.Idle, 3) with { Selected = 1 });

        Assert.StartsWith("  A-1", lines[2]);
        Assert.StartsWith("> A-2", lines[3]);
        Assert.StartsWith("  A-3", lines[4]);
    }

    [Fact]
    public void GivenWideSummaryAndTinySizes_WhenBuilding_ThenEveryRowKeepsTheWidth()
    {
        var tab = Tab(LoadState.Idle, 5) with { Rows = [Issue(1, "日本語日本語😀😀 combining é")] };
        var state = Active(tab);

        for (var width = 1; width <= 40; width++)
        {
            for (var height = 1; height <= 8; height++)
            {
                var lines = FrameBuilder.Build(state, width, height, _helpLines, _keymap);

                Assert.Equal(height, lines.Count);
                Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
            }
        }
    }

    private static IReadOnlyList<string> Frame(TabState tab)
    {
        return FrameBuilder.Build(Active(tab), 60, 10, _helpLines, _keymap);
    }

    private static AppState Active(TabState tab)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static TabState Tab(LoadState load, int rows = 0)
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = TabState.Opened(new TabId(1), definition, 1, 1, 10);

        return opened with { Load = load, Rows = [.. Enumerable.Range(1, rows).Select(number => Issue(number))] };
    }

    private static IssueSummary Issue(int number, string summary = "Summary")
    {
        return new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
            $"A-{number}",
            summary,
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }
}
