using Geeratya.Core.Boards;
using Geeratya.Core.Lists;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

public sealed partial class ColumnsBodyRenderer
{
    /// <summary>The cells between two borders: the column whose pane holds the run starting at <paramref name="start"/>.</summary>
    private static List<StyledSpan> Cell(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        BoardWindow window,
        List<PaneRect> panes,
        int start,
        int length,
        int row,
        bool compact,
        HashSet<string> marks,
        HashSet<string> expanded)
    {
        var position = 0;

        while (position + 1 < panes.Count && panes[position + 1].Column < start)
        {
            position++;
        }

        var column = window.First + position;
        var spans = new List<StyledSpan>();

        AddCell(spans, columns[column], cursor, column, row, compact, length, marks, expanded);

        return spans;
    }

    private static void AddCell(
        List<StyledSpan> spans,
        BoardColumnGroup group,
        BoardCursor cursor,
        int column,
        int row,
        bool compact,
        int inner,
        HashSet<string> marks,
        HashSet<string> expanded)
    {
        if (group.Rows.Count == 0)
        {
            spans.Add(row == 0
                ? new StyledSpan(TextWidth.Pad($" {NoCardsText}", inner), TextRole.Muted)
                : new StyledSpan(new string(' ', inner), TextRole.Body));

            return;
        }

        var pitch = compact ? 1 : BoardCards.FullRows;
        var card = row == BlankRow ? BlankRow : cursor.Scrolls[column] + (row / pitch);
        var within = row % pitch;

        if (card >= group.Rows.Count)
        {
            spans.Add(new StyledSpan(new string(' ', inner), TextRole.Body));

            return;
        }

        var selected = column == cursor.Column && card == cursor.Cards[column];
        var quiet = column != cursor.Column && card == Math.Clamp(cursor.Wanted, 0, group.Rows.Count - 1);
        var issue = group.Rows[card];
        var marked = marks.Contains(issue.Key);
        var open = issue.Subtasks.Count > 0 && expanded.Contains(issue.Key);

        if (compact)
        {
            var role = selected ? TextRole.Selected : quiet ? TextRole.SelectedDim : TextRole.Body;

            var nested = group.IsNested(card);
            var more = group.IsNested(card + 1);
            var line = TextWidth.Pad(CompactLine(issue, selected, marked, nested, more, inner), inner);
            var at = nested ? 2 : 1;

            if (selected || quiet || line.Length < at + 1)
            {
                spans.Add(new StyledSpan(line, role));
            }
            else
            {
                spans.Add(new StyledSpan(line[..at], role));
                spans.Add(new StyledSpan(line.Substring(at, 1), IssueTypeRole.For(issue.IssueType)));
                spans.Add(new StyledSpan(line[(at + 1)..], role));
            }

            return;
        }

        var part = group.IsNested(card)
            ? (group.IsNested(card + 1) ? GroupPart.Middle : GroupPart.Last)
            : (group.IsNested(card + 1) ? GroupPart.First : GroupPart.None);

        var nextSelected = column == cursor.Column && card + 1 == cursor.Cards[column] && group.IsNested(card + 1);

        var first = spans.Count;

        AddBoxRow(spans, issue, within, selected, marked, open, part, inner, selected || nextSelected);

        if (quiet)
        {
            Quieten(spans, first);
        }
    }

    private static bool IsQuietable(TextRole role)
    {
        return role is TextRole.Body or TextRole.Muted or TextRole.Strong or TextRole.TypeEpic or TextRole.TypeStory
            or TextRole.TypeBug or TextRole.TypeTask or TextRole.TypeSubtask;
    }

    /// <summary>
    /// Marks the card of a column without focus: the text and frame cells of the row become the quiet selection (the
    /// one-cell margins on each side stay on the page).
    /// </summary>
    private static void Quieten(List<StyledSpan> spans, int first)
    {
        for (var index = first + 1; index < spans.Count - 1; index++)
        {
            if (IsQuietable(spans[index].Role))
            {
                spans[index] = spans[index] with { Role = TextRole.SelectedDim };
            }
        }
    }

    /// <summary>Where a card sits in the shared frame of a parent and its children in one column.</summary>
    private enum GroupPart
    {
        /// <summary>A card of its own, with its own box.</summary>
        None,

        /// <summary>The parent: it opens the shared frame.</summary>
        First,

        /// <summary>A child with more children after it in the frame.</summary>
        Middle,

        /// <summary>The last child: it closes the shared frame.</summary>
        Last,
    }
}
