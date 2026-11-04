using Geeratya.Core.Boards;
using Geeratya.Core.Lists;
using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;

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
        HashSet<string> expanded,
        FlashCards? flash)
    {
        var position = 0;

        while (position + 1 < panes.Count && panes[position + 1].Column < start)
        {
            position++;
        }

        var column = window.First + position;
        var spans = new List<StyledSpan>();

        AddCell(spans, columns[column], cursor, column, row, compact, length, marks, expanded, flash);

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
        HashSet<string> expanded,
        FlashCards? flash)
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
            var start = spans.Count;

            AddCompact(spans, issue, group, card, selected, quiet, marked, inner);
            DrawFlash(spans, start, flash, issue, true, false);

            return;
        }

        var part = group.IsNested(card)
            ? (group.IsNested(card + 1) ? GroupPart.Middle : GroupPart.Last)
            : (group.IsNested(card + 1) ? GroupPart.First : GroupPart.None);

        var nextSelected = column == cursor.Column && card + 1 == cursor.Cards[column] && group.IsNested(card + 1);

        var first = spans.Count;

        AddBoxRow(spans, issue, within, selected, marked, open, part, inner, selected || nextSelected);

        if (selected)
        {
            Tint(spans, first);
        }

        var firstText = part is GroupPart.None or GroupPart.First ? 1 : 0;

        if (quiet && within == firstText)
        {
            Mark(spans, first);
        }

        DrawFlash(spans, first, flash, issue, within == firstText, true);
    }

    /// <summary>
    /// Draws the flash jump on a card row: the match strong and, on the key row (or the one line of a compact card),
    /// the letter over the cell before the type name. A card without a label is left alone.
    /// </summary>
    private static void DrawFlash(
        List<StyledSpan> spans,
        int first,
        FlashCards? flash,
        IssueSummary issue,
        bool letterRow,
        bool full)
    {
        if (flash is null || !flash.Labels.TryGetValue(issue.Key, out var label))
        {
            return;
        }

        var line = new StyledLine(spans.GetRange(first, spans.Count - first));
        var plain = line.PlainText;
        var from = label.LabelColumn + 1;
        var start = full && letterRow
            ? MatchInKey(plain, issue.Key, flash.Text, from)
            : plain.IndexOf(flash.Text, from, StringComparison.OrdinalIgnoreCase);
        var width = start < 0 ? 0 : DisplayWidth.Of(plain.Substring(start, flash.Text.Length));
        var column = start < 0 ? 0 : DisplayWidth.Of(plain[..start]);
        var drawn = label with
        {
            MatchColumn = column,
            MatchWidth = width,
            LabelColumn = letterRow ? label.LabelColumn : -1,
        };

        spans.RemoveRange(first, spans.Count - first);
        spans.AddRange(FlashOverlay.Apply(line, drawn).Spans);
    }

    /// <summary>Where the typed text sits inside the key on the key row, -1 when it is not part of the key.</summary>
    private static int MatchInKey(string plain, string key, string text, int from)
    {
        var keyAt = plain.IndexOf(key, from, StringComparison.Ordinal);
        var inKey = keyAt < 0 ? -1 : key.IndexOf(text, StringComparison.OrdinalIgnoreCase);

        return inKey < 0 ? -1 : keyAt + inKey;
    }

    /// <summary>
    /// One-line card: only the cursor card is tinted, and it keeps the issue-type colour of its marker; a card the
    /// cursor would land on in another column has just the quiet <c>&gt;</c>.
    /// </summary>
    private static void AddCompact(
        List<StyledSpan> spans,
        IssueSummary issue,
        BoardColumnGroup group,
        int card,
        bool selected,
        bool quiet,
        bool marked,
        int inner)
    {
        var nested = group.IsNested(card);
        var more = group.IsNested(card + 1);
        var line = TextWidth.Pad(CompactLine(issue, selected || quiet, marked, nested, more, inner), inner);
        var at = nested ? 2 : 1;
        var ground = selected ? SpanGround.Selection : SpanGround.Page;

        if (line.Length < at + 1)
        {
            spans.Add(new StyledSpan(line, TextRole.Body, ground));

            return;
        }

        var markRole = quiet ? TextRole.SelectedDim : TextRole.Body;

        spans.Add(new StyledSpan(line[..1], markRole, ground));

        if (at > 1)
        {
            spans.Add(new StyledSpan(line[1..at], TextRole.Body, ground));
        }

        spans.Add(new StyledSpan(line.Substring(at, 1), IssueTypeRole.For(issue.IssueType), ground));
        spans.Add(new StyledSpan(line[(at + 1)..], TextRole.Body, ground));
    }

    /// <summary>
    /// The selected card: its text and type colour sit on the selection tint (<see cref="SpanGround.Selection"/>), so
    /// the type colour survives; the frame keeps the focus accent and the margins stay on the page.
    /// </summary>
    private static void Tint(List<StyledSpan> spans, int first)
    {
        for (var index = first + 1; index < spans.Count - 1; index++)
        {
            var span = spans[index];

            if (span.Role == TextRole.Selected)
            {
                spans[index] = span with { Role = TextRole.Body, Ground = SpanGround.Selection };
            }
            else if (IsQuietable(span.Role))
            {
                spans[index] = span with { Ground = SpanGround.Selection };
            }
        }
    }

    /// <summary>
    /// The quiet <c>&gt;</c> in the left margin of the first text row of the card the cursor would land on in a column
    /// without focus; the card itself keeps its own look.
    /// </summary>
    private static void Mark(List<StyledSpan> spans, int first)
    {
        if (spans[first].Text == Blank)
        {
            spans[first] = new StyledSpan(SelectedMark, TextRole.SelectedDim);
        }
    }

    private static bool IsQuietable(TextRole role)
    {
        return role is TextRole.Body or TextRole.Muted or TextRole.Strong or TextRole.TypeEpic or TextRole.TypeStory
            or TextRole.TypeBug or TextRole.TypeTask or TextRole.TypeSubtask or TextRole.StatusToDo
            or TextRole.StatusInProgress or TextRole.StatusDone;
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
