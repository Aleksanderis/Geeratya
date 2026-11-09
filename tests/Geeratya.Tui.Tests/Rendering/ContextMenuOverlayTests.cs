using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ContextMenuOverlayTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenTheOpenMenu_WhenReadingTheRegions_ThenEachItemHasARowWithItsTitle(int width, int height)
    {
        var frame = FrameBuilder.BuildStyled(Sample(), width, height, _keymap)!;
        var rows = RegionProbe.Rows(frame, MouseTarget.ContextMenu);

        Assert.Equal([0, 1, 2], rows.Select(row => row.Index));
        Assert.Contains("Show or hide help", rows[0].Text, StringComparison.Ordinal);
        Assert.Contains("Quit", rows[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenMenu_WhenBuildingTheFrame_ThenTheKeysOfTheActionsAreShown()
    {
        var frame = FrameBuilder.BuildStyled(Sample(), 100, 24, _keymap)!;
        var text = string.Join('\n', frame.PlainLines());
        var quit = RegionProbe.Rows(frame, MouseTarget.ContextMenu).Single(row => row.Index == 2);

        Assert.Contains("Actions", text, StringComparison.Ordinal);
        Assert.Contains("  q ", quit.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenMenu_WhenClickingARow_ThenThatActionRunsAndTheMenuCloses()
    {
        var state = Sample();
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.ContextMenu).Single(candidate => candidate.Index == 0);

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        Assert.Null(result.State.ContextMenu);
        Assert.Equal(InputMode.Help, result.State.Mode);
    }

    [Fact]
    public void GivenALongMenuWithSeveralPrefixes_WhenBuildingTheFrame_ThenHeadersShowAndOnlyItemsHaveIndexes()
    {
        var items = new[]
        {
            ActionIds.IssueEdit, ActionIds.IssueDelete, ActionIds.IssueLink, ActionIds.RankUp, ActionIds.RankDown,
            ActionIds.RankTop, ActionIds.Quit,
        }.Select(action => new ContextMenuItem(action, ActionCatalog.FindTitle(action)!)).ToList();
        var state = Sample() with { ContextMenu = new ContextMenuState(items, 6) };
        var frame = FrameBuilder.BuildStyled(state, 100, 30, _keymap)!;
        var text = string.Join('\n', frame.PlainLines());
        var rows = RegionProbe.Rows(frame, MouseTarget.ContextMenu);

        Assert.Contains(" Issue ", text, StringComparison.Ordinal);
        Assert.Contains(" Rank ", text, StringComparison.Ordinal);
        Assert.Contains(" Quit ", text, StringComparison.Ordinal);
        Assert.Equal(Enumerable.Range(0, 7), rows.Select(row => row.Index));
    }

    [Fact]
    public void GivenAHeadedMenu_WhenBuildingTheFrame_ThenABlankRowSeparatesGroupsButNotTheFirstHeader()
    {
        var items = new[]
        {
            ActionIds.IssueEdit, ActionIds.IssueDelete, ActionIds.IssueLink, ActionIds.RankUp, ActionIds.RankDown,
            ActionIds.RankTop, ActionIds.Quit,
        }.Select(action => new ContextMenuItem(action, ActionCatalog.FindTitle(action)!)).ToList();
        var state = Sample() with { ContextMenu = new ContextMenuState(items, 6) };
        var lines = FrameBuilder.BuildStyled(state, 100, 30, _keymap)!.PlainLines().ToList();
        var issue = lines.FindIndex(line => line.Contains(" Issue ", StringComparison.Ordinal));
        var rank = lines.FindIndex(line => line.Contains(" Rank ", StringComparison.Ordinal));
        var other = lines.FindIndex(line => line.Contains(" App ", StringComparison.Ordinal));
        var column = lines[rank].IndexOf("Rank", StringComparison.Ordinal);

        // Eight columns from the header text stay inside the box, so only the menu's own row is checked.

        Assert.Contains("Actions", lines[issue - 1], StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(lines[rank - 1].Substring(column, 8)));
        Assert.True(other > rank);
        Assert.True(string.IsNullOrWhiteSpace(lines[other - 1].Substring(column, 8)));
    }

    [Fact]
    public void GivenAnyTerminalSize_WhenBuildingTheFrame_ThenItNeverThrows()
    {
        for (var width = 0; width <= 30; width++)
        {
            for (var height = 0; height <= 12; height++)
            {
                FrameBuilder.BuildStyled(Sample(), width, height, _keymap);
            }
        }
    }

    internal static AppState Sample()
    {
        var items = new List<ContextMenuItem>
        {
            new(ActionIds.HelpToggle, "Show or hide help"),
            new(ActionIds.IssueEdit, "Edit the issue"),
            new(ActionIds.Quit, "Quit"),
        };

        return AppState.Initial with
        {
            Mode = InputMode.Insert,
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
            ContextMenu = new ContextMenuState(items, 0),
        };
    }
}
