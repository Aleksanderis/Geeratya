using System.Globalization;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class TabBarTests
{
    private static readonly int[] _widths = [120, 80, 40];
    private static readonly int[] _tinyWidths = [20, 12, 1];

    [Fact]
    public void GivenNoTabs_WhenBuilding_ThenTheBarIsEmpty()
    {
        var line = TabBar.Build([], null, 80);

        Assert.Equal(string.Empty, line.PlainText);
    }

    [Fact]
    public void GivenOneTab_WhenBuilding_ThenItIsNumberedBracketedAndSelectedRole()
    {
        var tabs = Tabs("project = A");

        var line = TabBar.Build(tabs, tabs[0].Id, 120);

        Assert.Equal(" [1 JQL: project = A]  +", line.PlainText);
        Assert.Contains(line.Spans, span => span.Role == TextRole.Selected && span.Text == "[1 JQL: project = A]");
    }

    [Fact]
    public void GivenSourceKinds_WhenBuilding_ThenTitlesCarryTheirPrefix()
    {
        var tabs = new[]
        {
            Tab(1, ViewSourceKind.Board, "GCM board"),
            Tab(2, ViewSourceKind.Backlog, "GCM board"),
            Tab(3, ViewSourceKind.SavedFilter, "Mine"),
        };

        var line = TabBar.Build(tabs, tabs[0].Id, 120);

        Assert.Equal(" [1 Board: GCM board]  2 Backlog: GCM board  3 Filter: Mine  +", line.PlainText);
    }

    [Fact]
    public void GivenLoadingAndFailedTabs_WhenBuilding_ThenTheyShowTheirMarks()
    {
        var loading = Tab(1, ViewSourceKind.Jql, "a") with { Load = new Loading(LoadReason.Open) };
        var failed = Tab(2, ViewSourceKind.Jql, "b") with { Load = new Failed(JiraErrorKind.Timeout, "x") };
        var idle = Tab(3, ViewSourceKind.Jql, "c");

        var line = TabBar.Build([loading, failed, idle], loading.Id, 120);

        Assert.Equal(" [1 JQL: a …]  2 JQL: b !  3 JQL: c  +", line.PlainText);
    }

    [Fact]
    public void GivenLongTitles_WhenBuilding_ThenInactiveCutAt24AndActiveAt40()
    {
        var longTitle = new string('x', 80);
        var tabs = Tabs(longTitle, longTitle);

        var line = TabBar.Build(tabs, tabs[0].Id, 200);

        var inactive = $"2 JQL: {new string('x', 18)}…";
        var active = $"[1 JQL: {new string('x', 34)}…]";
        Assert.Equal($" {active}  {inactive}  +", line.PlainText);
    }

    [Fact]
    public void GivenTitlesThatDoNotFitAt24_WhenBuilding_ThenInactiveTitlesShrinkBeforeAnyTabIsHidden()
    {
        var longTitle = new string('x', 80);
        var tabs = Tabs(longTitle, longTitle, longTitle, longTitle);

        var line = TabBar.Build(tabs, tabs[0].Id, 100);

        Assert.DoesNotContain("‹", line.PlainText);
        Assert.DoesNotContain("›", line.PlainText);
        Assert.Contains("4 JQL:", line.PlainText);
        Assert.True(DisplayWidth.Of(line.PlainText) <= 100);
        Assert.True(DisplayWidth.Of(line.PlainText) > 80);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(30)]
    public void GivenAnyTabCountAndActiveTab_WhenBuildingAtEachSize_ThenTheBarFitsAndShowsTheActiveTab(int count)
    {
        foreach (var width in _widths)
        {
            for (var active = 0; active < count; active++)
            {
                var tabs = Tabs(Enumerable.Range(1, count).Select(index => $"project = P{index}"));

                var line = TabBar.Build(tabs, tabs[active].Id, width);

                Assert.True(DisplayWidth.Of(line.PlainText) <= width, $"{count} tabs, {width} wide, tab {active}");
                Assert.Contains(line.Spans, span => span.Role == TextRole.Selected && span.Text.StartsWith($"[{active + 1} "));
            }
        }
    }

    [Fact]
    public void GivenTinyWidths_WhenBuilding_ThenTheBarStillNamesTheActiveTabWithoutFailing()
    {
        var tabs = Tabs(Enumerable.Range(1, 30).Select(index => $"project = P{index}"));

        foreach (var width in _tinyWidths)
        {
            var line = TabBar.Build(tabs, tabs[14].Id, width);

            Assert.Contains(line.Spans, span => span.Role == TextRole.Selected && span.Text.StartsWith("[15 "));
        }
    }

    [Fact]
    public void GivenThirtyTabsAt120_WhenTheMiddleOneIsActive_ThenHiddenCountsMarkBothEnds()
    {
        var tabs = Tabs(Enumerable.Range(1, 30).Select(index => $"project = GCM{index}"));

        var line = TabBar.Build(tabs, tabs[14].Id, 120);

        var text = line.PlainText;
        Assert.StartsWith(" ‹", text);
        Assert.EndsWith(" ›", text);
        Assert.Contains("[15 JQL:", text);
        var shown = Enumerable.Range(1, 30).Count(number => text.Contains($" {number} JQL:") || text.Contains($"[{number} JQL:"));
        var left = int.Parse(text[" ‹".Length..text.IndexOf(' ', 2)], CultureInfo.InvariantCulture);
        var plus = text.LastIndexOf('+');
        var right = int.Parse(text[(plus + 1)..text.IndexOf(' ', plus)], CultureInfo.InvariantCulture);
        Assert.Equal(30, left + shown + right);
    }

    [Fact]
    public void GivenThirtyTabsAt120_WhenTheFirstOneIsActive_ThenOnlyTheRightEndIsMarked()
    {
        var tabs = Tabs(Enumerable.Range(1, 30).Select(index => $"project = GCM{index}"));

        var line = TabBar.Build(tabs, tabs[0].Id, 120);

        Assert.DoesNotContain("‹", line.PlainText);
        Assert.EndsWith(" ›", line.PlainText);
    }

    [Fact]
    public void GivenTheLastOfThirtyTabsAt40_WhenBuilding_ThenOnlyTheLeftEndIsMarkedAndTheActiveTitleIsCut()
    {
        var tabs = Tabs(Enumerable.Range(1, 30).Select(index => $"project = GCM-LONG-NAME-{index}"));

        var line = TabBar.Build(tabs, tabs[29].Id, 40);

        Assert.StartsWith(" ‹", line.PlainText);
        Assert.DoesNotContain("›", line.PlainText);
        Assert.Contains("[30 JQL:", line.PlainText);
        Assert.Contains("…", line.PlainText);
        Assert.True(DisplayWidth.Of(line.PlainText) <= 40);
    }

    [Fact]
    public void GivenWideCharacterTitles_WhenCutting_ThenNoCharacterIsSplitAndWidthHolds()
    {
        var tabs = Tabs("日本語日本語日本語日本語日本語日本語", "日本語日本語日本語日本語日本語日本語", "ééééééééééé");

        foreach (var width in _widths)
        {
            var line = TabBar.Build(tabs, tabs[1].Id, width);

            Assert.True(DisplayWidth.Of(line.PlainText) <= width);
            Assert.DoesNotContain('�', line.PlainText);
        }
    }

    [Fact]
    public void GivenAMultilineQuery_WhenBuilding_ThenTheTitleIsOneLine()
    {
        var tabs = Tabs("project = A\nORDER BY key");

        var line = TabBar.Build(tabs, tabs[0].Id, 120);

        Assert.DoesNotContain('\n', line.PlainText);
        Assert.Contains("project = A ORDER BY key", line.PlainText);
    }

    [Fact]
    public void GivenCut_WhenWidthIsZeroOrOne_ThenItIsEmptyOrAnEllipsis()
    {
        Assert.Equal(string.Empty, TabTitles.Cut("abc", 0));
        Assert.Equal("…", TabTitles.Cut("abc", 1));
        Assert.Equal("ab…", TabTitles.Cut("abcd", 3));
        Assert.Equal("abc", TabTitles.Cut("abc", 3));
    }

    private static TabState Tab(long id, ViewSourceKind kind, string title)
    {
        var definition = new ViewDefinition(kind, title, ViewLayout.List, title);

        return TabState.Opened(new TabId(id), definition, id, id, 10) with { Load = LoadState.Idle };
    }

    private static List<TabState> Tabs(params IEnumerable<string> queries)
    {
        return [.. queries.Select((query, index) => Tab(index + 1, ViewSourceKind.Jql, query))];
    }
}
