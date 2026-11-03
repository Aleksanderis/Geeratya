using System.Globalization;
using Geeratya.Core.Keymap;
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
    public void GivenOneTab_WhenBuilding_ThenItIsANumberedPaddedChipInTheActiveRole()
    {
        var tabs = Tabs("project = A");

        var line = TabBar.Build(tabs, tabs[0].Id, 120);

        Assert.Equal(" 1 JQL project = A x   + ", line.PlainText);
        Assert.Contains(line.Spans, span => span.Role == TextRole.TabActive && span.Text == " 1 JQL project = A x ");
        Assert.Contains(line.Spans, span => span.Role == TextRole.BarMuted && span.Text == " + ");
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

        Assert.Equal(" 1 Board GCM board x   2 Backlog GCM board   3 Filter Mine   + ", line.PlainText);
    }

    [Fact]
    public void GivenLoadingAndFailedTabs_WhenBuilding_ThenTheyShowTheirMarks()
    {
        var loading = Tab(1, ViewSourceKind.Jql, "a") with { Load = new Loading(LoadReason.Open) };
        var failed = Tab(2, ViewSourceKind.Jql, "b") with { Load = new Failed(JiraErrorKind.Timeout, "x") };
        var idle = Tab(3, ViewSourceKind.Jql, "c");

        var line = TabBar.Build([loading, failed, idle], loading.Id, 120);

        Assert.Equal(" 1 JQL a … x   2 JQL b !   3 JQL c   + ", line.PlainText);
    }

    [Fact]
    public void GivenLongTitles_WhenBuilding_ThenEveryLabelIsCutAt24()
    {
        var longTitle = new string('x', 80);
        var tabs = Tabs(longTitle, longTitle);

        var line = TabBar.Build(tabs, tabs[0].Id, 200);

        var chip = $"JQL {new string('x', 19)}…";
        Assert.StartsWith($" 1 {chip} x   2 {chip} ", line.PlainText);
    }

    [Fact]
    public void GivenTitlesThatDoNotFitAt24_WhenBuilding_ThenInactiveTitlesShrinkBeforeAnyTabIsHidden()
    {
        var longTitle = new string('x', 80);
        var tabs = Tabs(longTitle, longTitle, longTitle, longTitle);

        var line = TabBar.Build(tabs, tabs[0].Id, 100);

        Assert.DoesNotContain("‹", line.PlainText);
        Assert.DoesNotContain("›", line.PlainText);
        Assert.Contains(" 4 JQL ", line.PlainText);
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
                Assert.Contains(line.Spans, span => span.Role == TextRole.TabActive && span.Text.StartsWith($" {active + 1} "));
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

            Assert.Contains(line.Spans, span => span.Role == TextRole.TabActive && span.Text.StartsWith(" 15 "));
        }
    }

    [Fact]
    public void GivenThirtyTabsAt120_WhenTheMiddleOneIsActive_ThenHiddenCountsMarkBothEnds()
    {
        var tabs = Tabs(Enumerable.Range(1, 30).Select(index => $"project = GCM{index}"));

        var line = TabBar.Build(tabs, tabs[14].Id, 120);

        var text = line.PlainText;
        Assert.StartsWith(" ‹", text);
        Assert.EndsWith(" › ", text);
        Assert.Contains(" 15 JQL ", text);
        var shown = Enumerable.Range(1, 30).Count(number => text.Contains($" {number} JQL "));
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
        Assert.EndsWith(" › ", line.PlainText);
    }

    [Fact]
    public void GivenTheLastOfThirtyTabsAt40_WhenBuilding_ThenOnlyTheLeftEndIsMarkedAndTheActiveTitleIsCut()
    {
        var tabs = Tabs(Enumerable.Range(1, 30).Select(index => $"project = GCM-LONG-NAME-{index}"));

        var line = TabBar.Build(tabs, tabs[29].Id, 40);

        Assert.StartsWith(" ‹", line.PlainText);
        Assert.DoesNotContain("›", line.PlainText);
        Assert.Contains(" 30 JQL ", line.PlainText);
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
        Assert.Contains("project = A ORDER B…", line.PlainText);
    }

    [Fact]
    public void GivenWideBar_WhenBuilding_ThenOnlyTheActiveChipCarriesTheCloseXWithItsOwnCells()
    {
        var tabs = Tabs("project = A", "project = B");

        var wide = TabBar.Areas(tabs, tabs[1].Id, 80);
        var narrow = TabBar.Areas(tabs, tabs[1].Id, 79);
        var line = TabBar.Build(tabs, tabs[1].Id, 80).PlainText;

        Assert.Null(wide[0].Close);
        var close = wide[1].Close!.Value;
        Assert.Equal(wide[1].End, close.End);
        Assert.Equal("x ", line[close.Start..close.End]);
        Assert.All(narrow, area => Assert.Null(area.Close));
        Assert.Equal(close, TabBar.CloseButton(tabs, tabs[1].Id, 80));
        Assert.Null(TabBar.CloseButton(tabs, tabs[1].Id, 79));
    }

    [Theory]
    [InlineData(39)]
    [InlineData(20)]
    public void GivenUnderFortyColumns_WhenBuilding_ThenOnlyTheActiveChipAndThePlusRemain(int width)
    {
        var tabs = Tabs("project = A", "project = B", "project = C");

        var line = TabBar.Build(tabs, tabs[1].Id, width);

        Assert.DoesNotContain("‹", line.PlainText);
        Assert.DoesNotContain("›", line.PlainText);
        Assert.Contains(" 2 JQL ", line.PlainText);
        Assert.DoesNotContain(" 1 JQL", line.PlainText);
        Assert.DoesNotContain(" 3 JQL", line.PlainText);
        Assert.Single(TabBar.Areas(tabs, tabs[1].Id, width));
    }

    [Fact]
    public void GivenTabs_WhenBuildingWithTheJumpChip_ThenTheBarIsFullWidthAndTheChipNamesTheKeys()
    {
        var tabs = Tabs("project = A");

        var line = TabBar.BuildWithJumpChip(tabs, tabs[0].Id, 80);

        Assert.Equal(80, DisplayWidth.Of(line.PlainText));
        Assert.EndsWith(" <space>g jump ", line.PlainText);
        Assert.Equal(TextRole.TabDim, line.Spans[^1].Role);
        Assert.Equal(80, DisplayWidth.Of(TabBar.BuildWithJumpChip([], null, 80).PlainText));
    }

    [Theory]
    [InlineData(44)]
    [InlineData(80)]
    [InlineData(120)]
    public void GivenSeveralTabs_WhenWidthIsFortyFourOrMore_ThenTheCommandsChipShowsAtTheRightEnd(int width)
    {
        var tabs = Tabs("project = A", "project = B", "project = C", "project = D");

        var line = TabBar.BuildWithJumpChip(tabs, tabs[0].Id, width);
        var chip = TabBar.CommandsChip(tabs, tabs[0].Id, width);

        Assert.NotNull(chip);
        Assert.Equal(width, DisplayWidth.Of(line.PlainText));
        Assert.Equal(" : commands ", line.PlainText.Substring(chip.Value.Start, chip.Value.End - chip.Value.Start));
    }

    [Fact]
    public void GivenWideBar_WhenBuilding_ThenTheCommandsChipSitsLeftOfTheJumpChip()
    {
        var tabs = Tabs("project = A");

        var line = TabBar.BuildWithJumpChip(tabs, tabs[0].Id, 120);

        Assert.EndsWith(" : commands   <space>g jump ", line.PlainText);
        Assert.Equal(120, TabBar.JumpChip(tabs, tabs[0].Id, 120)!.Value.End);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(43)]
    public void GivenNarrowBar_WhenWidthIsUnderFortyFour_ThenThereIsNoCommandsChip(int width)
    {
        var tabs = Tabs("project = A");

        Assert.Null(TabBar.CommandsChip(tabs, tabs[0].Id, width));
        Assert.DoesNotContain("commands", TabBar.BuildWithJumpChip(tabs, tabs[0].Id, width).PlainText);
    }

    [Fact]
    public void GivenManyTabs_WhenWidthIsFortyFour_ThenTheTabsLeaveRoomAndTheJumpChipGivesWay()
    {
        var tabs = Tabs("project = A", "project = B", "project = C", "project = D", "project = E", "project = F");

        var line = TabBar.BuildWithJumpChip(tabs, tabs[0].Id, 44);

        Assert.EndsWith(" : commands ", line.PlainText);
        Assert.Null(TabBar.JumpChip(tabs, tabs[0].Id, 44));
        Assert.Equal(44, DisplayWidth.Of(line.PlainText));
        Assert.All(TabBar.Areas(tabs, tabs[0].Id, 44), area => Assert.True(area.End <= 44 - 13));
    }

    [Fact]
    public void GivenAKeymapWithoutThePaletteKey_WhenBuilding_ThenTheChipDropsTheKeyAndStaysClickable()
    {
        var tabs = Tabs("project = A");
        var keymap = new KeymapTable([]);

        var line = TabBar.BuildWithJumpChip(tabs, tabs[0].Id, 80, keymap);
        var chip = TabBar.CommandsChip(tabs, tabs[0].Id, 80, keymap)!.Value;

        Assert.Equal(" commands ", line.PlainText.Substring(chip.Start, chip.End - chip.Start));
    }

    [Fact]
    public void GivenARemappedPaletteKey_WhenBuilding_ThenTheChipNamesIt()
    {
        var tabs = Tabs("project = A");
        var keymap = new KeymapTable([new KeyBinding(InputMode.Normal, "Ctrl-p", ActionIds.PaletteOpen)]);

        Assert.Contains(" Ctrl-p commands ", TabBar.BuildWithJumpChip(tabs, tabs[0].Id, 80, keymap).PlainText);
    }

    [Theory]
    [InlineData(44)]
    [InlineData(48)]
    public void GivenSeveralTabs_WhenWidthIsFortyFourOrFortyEight_ThenTheBarKeepsItsHiddenTabMarks(int width)
    {
        var tabs = Tabs("project = A", "project = B", "project = C", "project = D");

        var line = TabBar.BuildWithJumpChip(tabs, tabs[1].Id, width);

        Assert.Contains("‹", line.PlainText);
        Assert.Contains("commands", line.PlainText);
        Assert.Equal(width, DisplayWidth.Of(line.PlainText));
    }

    [Fact]
    public void GivenNoTabs_WhenWide_ThenTheCommandsChipStillShows()
    {
        var line = TabBar.BuildWithJumpChip([], null, 80);

        Assert.EndsWith(" : commands ", line.PlainText);
        Assert.Null(TabBar.JumpChip([], null, 80));
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
