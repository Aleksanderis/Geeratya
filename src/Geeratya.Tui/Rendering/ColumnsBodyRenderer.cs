using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The body of the active tab in the columns layout (<c>docs/ux-screens.md</c>, "Columns layout"): the title with
/// the count, one header per board column and then only the cards in the viewport of each drawn column. Columns that
/// do not fit are left out around the selected one. Only the visible cards are drawn, never all the loaded ones.
/// </summary>
public sealed partial class ColumnsBodyRenderer : IBodyRenderer
{
    private const int MinTypeName = 4;
    private const string NoCardsText = "no issues";
    private const string NoSprintText = "No active sprint on this board.";
    private const string HiddenLeftMark = "‹";
    private const string HiddenRightMark = "›";
    private const string SelectedMark = ">";
    private const string MarkedMark = "*";
    private const string Blank = " ";
    private const int FirstSummaryLine = 2;
    private const int BlankRow = int.MaxValue;
    private const int Margin = 1;
    private const int ChildIndent = 2;

    private const int SubtaskBarCells = 6;
    private const int MinimumBoxWidth = 8;
    private const string ParentMark = "↑";
    private const string Vertical = "│";
    private const char HorizontalChar = '─';

    public bool AppliesTo(AppState state)
    {
        var tab = ListBodyRenderer.ActiveTab(state);

        return state.Mode != InputMode.Help && tab is { Definition.Layout: ViewLayout.Columns };
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ListBodyRenderer.ActiveTab(state)!;
        var glyphs = Glyphs.For(context.Glyphs);
        var area = Math.Max(0, height - ListLayout.HeaderRows - ListLayout.FooterRows);
        var columns = BusyMarks.Mark(ColumnsNavigation.ColumnsOf(tab), BusyMarks.Keys(state), glyphs);
        var cards = BoardCards.Visible(area, ListLayout.HeaderRows);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, cards);
        var window = BoardWindows.For(columns.Count, cursor.Column, width);

        if (tab.Rows.Count == 0 || columns.Count == 0)
        {
            return Message(tab, context, width, height, glyphs);
        }

        if (!FrameGrid.CanFrame(width, height))
        {
            return Bare(tab, columns, cursor, width, height);
        }

        var compact = BoardCards.IsCompact(area, ListLayout.HeaderRows);
        var cardRows = BoardCards.CardRows(area, ListLayout.HeaderRows);
        var marks = tab.Marks.Count == 0 ? [] : new HashSet<string>(tab.Marks, StringComparer.OrdinalIgnoreCase);
        var expanded = new HashSet<string>(tab.ExpandedSubtasks, StringComparer.Ordinal);
        var panes = Panes(window, width, height);
        var grid = new FrameGrid(width, height, panes, cursor.Column - window.First);
        var lines = new List<StyledLine>(height) { TopEdge(tab, columns, cursor, window, grid, panes) };

        for (var row = 0; row < cardRows; row++)
        {
            var card = row < cards * (compact ? 1 : BoardCards.FullRows) ? row : BlankRow;
            var spans = grid.Row(
                row + BoardCards.TopRows,
                (start, length) => Cell(columns, cursor, window, panes, start, length, card, compact, marks, expanded));

            lines.Add(new StyledLine(spans));
        }

        lines.Add(new StyledLine(grid.Row(height - 1, static (_, _) => [])));

        return lines;
    }

    /// <summary>The empty and loading states: the message lines inside the frame, the board title as the chip.</summary>
    private static List<StyledLine> Message(TabState tab, BodyContext context, int width, int height, Glyphs glyphs)
    {
        var lines = new List<StyledLine>();

        if (NoActiveSprint(tab))
        {
            lines.AddRange(StateBody.Empty(NoSprintText, NoSprintHint(context.Keymap)));
        }
        else
        {
            ListBodyRenderer.AddBodyMessage(lines, tab, context, glyphs);
        }

        var title = new List<StyledSpan> { new(tab.Definition.Title, TextRole.Heading) };

        return PaneFrame.Build(width, height, title, null, lines, []);
    }

    /// <summary>Below the size that gets a frame: the selected column as one-line cards under its title.</summary>
    private static List<StyledLine> Bare(
        TabState tab,
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        int width,
        int height)
    {
        var group = columns[cursor.Column];
        var lines = new List<StyledLine> { StyledLine.Of(ColumnTitle(tab, group), TextRole.Heading) };
        var first = cursor.Scrolls[cursor.Column];

        for (var card = first; card < group.Rows.Count && lines.Count < height; card++)
        {
            var selected = card == cursor.Cards[cursor.Column];
            var role = selected ? TextRole.Selected : TextRole.Body;

            lines.Add(StyledLine.Of(CompactLine(group.Rows[card], selected, false, false, false, width), role));
        }

        return lines;
    }

    /// <summary>The panes of the window: one box per drawn column, sharing the edge with the next.</summary>
    private static List<PaneRect> Panes(BoardWindow window, int width, int height)
    {
        var panes = new List<PaneRect>(window.Count);
        var column = 0;

        for (var position = 0; position < window.Count; position++)
        {
            var last = position == window.Count - 1;
            var right = last ? width - 1 : column + window.ColumnWidth(position);

            panes.Add(new PaneRect(0, column, height, right - column + 1));
            column = right;
        }

        return panes;
    }
}
