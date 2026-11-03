using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class TabBarFrameTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenThreeTabs_WhenBuildingTheFrame_ThenTheFirstLineIsTheBarAndTheFrameFillsTheSize(int width, int height)
    {
        var lines = FrameBuilder.Build(State(3, 30), width, height, _keymap);

        Assert.Equal(height, lines.Count);
        Assert.Contains("[3 JQL:", lines[0]);
        Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
    }

    [Theory]
    [InlineData(36)]
    [InlineData(24)]
    [InlineData(10)]
    public void GivenALoadedTab_WhenBuildingTheFrame_ThenIssueRowsShownEqualTheViewportTheHostReports(int height)
    {
        var lines = FrameBuilder.Build(State(1, 50), 80, height, _keymap);

        var shown = lines.Count(line => line.Contains("A-", StringComparison.Ordinal));
        var bodyRows = height - 1 - FrameBuilder.HeaderRows - ListLayout.HeaderRows - ListLayout.FooterRows;
        Assert.Equal(bodyRows, shown);
    }

    [Fact]
    public void GivenNoTabs_WhenBuildingTheFrame_ThenTheTabBarLineIsBlank()
    {
        var lines = FrameBuilder.Build(AppState.Initial, 40, 10, _keymap);

        Assert.Equal(new string(' ', 40), lines[0]);
    }

    [Fact]
    public void GivenTabs_WhenBuildingStyledFrame_ThenTheActiveTabUsesTheSelectedRole()
    {
        var frame = FrameBuilder.BuildStyled(State(2, 5), 80, 12, _keymap)!;

        Assert.Contains(frame.Header[0].Spans, span => span.Role == TextRole.Selected && span.Text.StartsWith("[2 "));
    }

    private static AppState State(int tabs, int rows)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile);
        var list = new List<TabState>();

        for (var index = 1; index <= tabs; index++)
        {
            var definition = new ViewDefinition(ViewSourceKind.Jql, $"project = P{index}", ViewLayout.List, $"project = P{index}");
            var items = Enumerable.Range(1, rows).Select(Issue).ToArray();

            list.Add(TabState.Opened(new TabId(index), definition, index, index, 10) with
            {
                Rows = items,
                Load = LoadState.Idle,
                KeyWidth = 4,
            });
        }

        session = session with { Tabs = list, Active = list[^1].Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static IssueSummary Issue(int number)
    {
        var key = $"A-{number}";

        return new IssueSummary(
            key,
            key,
            $"Issue {number}",
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
