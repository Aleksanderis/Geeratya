using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Detail;
using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Selection;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The sub-task row of a board card and its expanded list (#520), on the recorded boards of both project kinds with
/// the <c>subtasks</c> stubs added to the parents.
/// </summary>
public class ColumnsSubtaskTests
{
    private const int Width = 120;
    private const int Height = 36;
    private const int Rows = Height - 1 - 2 - 2 - 1;

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly AppReducer _reducer = new(_keymap);

    [Theory]
    [InlineData("gcm", "GCM-10", "Sub-tasks 1/2")]
    [InlineData("gtm", "GTM-11", "Sub-tasks 1/2")]
    public async Task GivenACardWithSubtasks_WhenCollapsed_ThenTheRowShowsDoneOverTotalAndTheFoldMark(
        string kind,
        string parent,
        string expected)
    {
        var state = await OpenAsync(kind, true, parent);

        var text = string.Join('\n', FrameBuilder.Build(state, Width, Height, _keymap));

        Assert.Contains(expected, text, StringComparison.Ordinal);
        Assert.Contains("▸ Sub-tasks", text, StringComparison.Ordinal);
        Assert.DoesNotContain("▾", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("gcm", "GCM-10")]
    [InlineData("gtm", "GTM-11")]
    public async Task GivenABoardFilterThatLeavesSubtasksOut_WhenExpanding_ThenTheyAreListedUnderTheParentOnly(
        string kind,
        string parent)
    {
        var state = await OpenAsync(kind, true, parent);
        var before = ColumnKeys(state);

        var opened = Press(state, "z", "a");
        var after = ColumnKeys(opened);

        Assert.DoesNotContain(before, key => IsSubtaskOf(state, key));
        Assert.Equal(before.Count + 2, after.Count);
        Assert.Equal(parent, after[before.IndexOf(parent)]);
        Assert.All(Listed(opened), row => Assert.True(row.IsListed));
        Assert.Equal(2, Listed(opened).Count);
        Assert.Contains("▾ Sub-tasks", string.Join('\n', FrameBuilder.Build(opened, Width, Height, _keymap)));
        Assert.Equal(ColumnCount(state), ColumnCount(opened));
    }

    [Theory]
    [InlineData("gcm", "GCM-10")]
    [InlineData("gtm", "GTM-11")]
    public async Task GivenAnExpandedCard_WhenMovingDownAndOpening_ThenTheListedSubtaskIsSelectedAndOpens(
        string kind,
        string parent)
    {
        var state = Press(await OpenAsync(kind, true, parent), "z", "a");

        var moved = Press(state, "j");
        var selected = ColumnsNavigation.SelectedIssue(Active(moved))!;
        var opening = _reducer.Reduce(moved, new KeyPressed("Enter"));

        Assert.True(selected.IsListed);
        Assert.Equal(parent, selected.Parent?.Key);
        Assert.Contains(opening.Effects.OfType<LoadIssueDetailEffect>(), effect => effect.Key == selected.Key);
    }

    [Fact]
    public async Task GivenTheCursorOnAListedSubtask_WhenToggling_ThenTheParentCollapsesAndIsSelected()
    {
        var state = Press(await OpenAsync("gcm", true, "GCM-10"), "z", "a", "j");

        var closed = Press(state, "z", "a");

        Assert.Equal("GCM-10", ColumnsNavigation.SelectedIssue(Active(closed))?.Key);
        Assert.Empty(Active(closed).ExpandedSubtasks);
        Assert.DoesNotContain(ColumnKeys(closed), key => IsSubtaskOf(closed, key));
    }

    [Fact]
    public async Task GivenABoardWhereSubtasksAreCardsToo_WhenExpanding_ThenNoSubtaskIsDuplicated()
    {
        var state = await OpenAsync("gcm", false, "GCM-10");
        var before = ColumnKeys(state);

        var opened = Press(state, "z", "a");

        Assert.Equal(before, ColumnKeys(opened));
        Assert.Empty(Listed(opened));
    }

    [Fact]
    public async Task GivenACardWithoutSubtasks_WhenToggling_ThenNothingExpandsAndTheStatusSaysSo()
    {
        var state = await OpenAsync("gcm", true, "GCM-9");

        var result = Press(state, "z", "a");

        Assert.Empty(Active(result).ExpandedSubtasks);
        Assert.Contains("no sub-tasks", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("gcm", true, "GCM-10")]
    [InlineData("gcm", false, "GCM-10")]
    [InlineData("gtm", true, "GTM-11")]
    [InlineData("gtm", false, "GTM-11")]
    public async Task GivenCollapsedAndExpandedBoards_WhenBuildingAtEverySize_ThenEveryLineKeepsTheWidth(
        string kind,
        bool filtered,
        string parent)
    {
        var collapsed = await OpenAsync(kind, filtered, parent);
        var expanded = Press(collapsed, "z", "a");

        foreach (var state in new[] { collapsed, expanded })
        {
            for (var width = 1; width <= 130; width += 3)
            {
                for (var height = 1; height <= 40; height += 3)
                {
                    var lines = FrameBuilder.Build(state, width, height, _keymap);

                    Assert.Equal(height, lines.Count);
                    Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
                }
            }
        }
    }

    [Theory]
    [InlineData("gcm", "GCM-10", false)]
    [InlineData("gcm", "GCM-10", true)]
    [InlineData("gtm", "GTM-11", false)]
    [InlineData("gtm", "GTM-11", true)]
    public async Task GivenAFilteredBoard_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(
        string kind,
        string parent,
        bool expand)
    {
        var state = await OpenAsync(kind, true, parent);
        state = expand ? Press(state, "z", "a") : state;

        var lines = FrameBuilder.Build(state, Width, Height, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var name = $"columns-subtasks-{kind}-{(expand ? "expanded" : "collapsed")}-{Width}x{Height}.txt";

        Assert.Equal(SnapshotFile.Expect(name, actual), actual);
    }

    [Fact]
    public async Task GivenTheAsciiGlyphSet_WhenBuildingAnExpandedCard_ThenTheFrameDrawsOnlyAscii()
    {
        var state = Press(await OpenAsync("gcm", true, "GCM-10"), "z", "a");

        var lines = FrameBuilder.Build(state, Width, Height, _keymap, null, GlyphSet.Ascii);

        Assert.All(lines, line => Assert.DoesNotContain(line, character => Glyphs.AsciiSwapped.Contains(character)));
        Assert.Contains(lines, line => line.Contains("v Sub-tasks", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenTheCursorOnAListedSubtask_WhenMarking_ThenItIsRefusedAndNothingIsMarked()
    {
        var state = Press(await OpenAsync("gcm", true, "GCM-10"), "z", "a", "j");

        var result = Press(state, "Alt-m");

        Assert.Empty(Active(result).Marks);
        Assert.Contains("cannot be marked", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheCursorOnAListedSubtask_WhenAskingForWriteTargets_ThenOnlyThatSubtaskIsTheTarget()
    {
        var state = Press(await OpenAsync("gcm", true, "GCM-10"), "z", "a", "j");

        var targets = RowSelection.Targets(Active(state));

        Assert.Equal(["GCM-18"], targets);
    }

    [Fact]
    public async Task GivenALaneThatHidesASubtaskCard_WhenExpanding_ThenTheSubtaskIsNotListedTwice()
    {
        var state = await OpenAsync("gcm", false, "GCM-10");
        var tab = Active(state);
        var lane = tab with { Swimlane = SwimlaneKind.Assignee, ExpandedSubtasks = ["GCM-10"] };
        var rows = tab.Rows.Where(row => row.Key != "GCM-18").Select(row => row).ToList();

        var columns = BoardColumns.Visible(tab.Board, rows, lane.ExpandedSubtasks, tab.Rows);

        Assert.DoesNotContain(columns.SelectMany(column => column.Rows), row => row.IsListed && row.Key == "GCM-18");
    }

    private static async Task<AppState> OpenAsync(string kind, bool filtered, string selectKey)
    {
        var state = await BoardStateFixtures.WithSubtasksAsync(kind, filtered, Rows);

        return Select(state, selectKey);
    }

    /// <summary>Walks the board with the keys (column by column, top to bottom) until the card is selected.</summary>
    private static AppState Select(AppState state, string key)
    {
        var current = Press(state, "0");

        for (var column = 0; column < ColumnsNavigation.ColumnsOf(Active(state)).Count; column++)
        {
            var top = Press(current, "g", "g");

            for (var step = 0; step < 30; step++)
            {
                if (ColumnsNavigation.SelectedIssue(Active(top))?.Key == key)
                {
                    return top;
                }

                top = Press(top, "j");
            }

            current = Press(current, "l");
        }

        throw new InvalidOperationException($"{key} is not on the board.");
    }

    private static AppState Press(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key)).State);
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.First(tab => tab.Id == state.Session.Active);
    }

    private static List<string> ColumnKeys(AppState state)
    {
        return [.. ColumnsNavigation.ColumnsOf(Active(state)).SelectMany(column => column.Rows).Select(row => row.Key)];
    }

    private static List<IssueSummary> Listed(AppState state)
    {
        var rows = ColumnsNavigation.ColumnsOf(Active(state)).SelectMany(column => column.Rows);

        return [.. rows.Where(row => row.IsListed)];
    }

    private static int ColumnCount(AppState state)
    {
        return ColumnsNavigation.ColumnsOf(Active(state)).Sum(column => column.Rows.Count(row => !row.IsListed));
    }

    private static bool IsSubtaskOf(AppState state, string key)
    {
        return Active(state).Rows.Any(row => row.Subtasks.Any(subtask => subtask.Key == key));
    }
}
