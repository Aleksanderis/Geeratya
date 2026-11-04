using Geeratya.Core.Boards;
using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Tui.Rendering;

public sealed partial class ColumnsBodyRenderer
{
    /// <summary>
    /// One row of a card, in a margin of one cell on each side of the column. A card of its own is a box; a parent
    /// and its children share one frame (the parent is its top, a divider follows each member and the last member's
    /// divider is the bottom of the frame; children are indented). A selected card has heavy sides and the
    /// <c>Selected</c> role on its text.
    /// </summary>
    private static void AddBoxRow(
        List<StyledSpan> spans,
        IssueSummary issue,
        CardRow row,
        CardGroupPart part,
        CardState state,
        int inner)
    {
        var width = inner - (2 * Margin);

        if (width < MinimumBoxWidth)
        {
            spans.Add(new StyledSpan(new string(' ', inner), TextRole.Body));

            return;
        }

        spans.Add(new StyledSpan(new string(' ', Margin), TextRole.Body));

        if (row.Kind is CardRowKind.Top or CardRowKind.Bottom or CardRowKind.Divider)
        {
            AddEdge(spans, issue, row, part, state, width);
        }
        else
        {
            AddTextRow(spans, issue, row, part, state, width);
        }

        spans.Add(new StyledSpan(Blank, TextRole.Body));
    }

    private static void AddEdge(
        List<StyledSpan> spans,
        IssueSummary issue,
        CardRow row,
        CardGroupPart part,
        CardState state,
        int width)
    {
        var top = row.Kind == CardRowKind.Top;
        var heavy = top ? state.Selected : state.Selected || state.NextSelected;
        var closing = row.Kind == CardRowKind.Bottom || part == CardGroupPart.Last;
        var (left, right) = (top, closing) switch
        {
            (true, _) => heavy ? ("┏", "┓") : ("┌", "┐"),
            (_, true) => heavy ? ("┗", "┛") : ("└", "┘"),
            _ => heavy ? ("┣", "┫") : ("├", "┤"),
        };
        var fill = heavy ? '━' : HorizontalChar;
        var label = top ? ParentLabel(issue, width - 2, fill) : string.Empty;
        var bar = label + new string(fill, width - 2 - TextWidth.Of(label));

        spans.Add(new StyledSpan($"{left}{bar}{right}", heavy ? TextRole.FrameFocus : TextRole.Muted));
    }

    private static void AddTextRow(
        List<StyledSpan> spans,
        IssueSummary issue,
        CardRow row,
        CardGroupPart part,
        CardState state,
        int width)
    {
        var side = state.Selected ? "┃" : Vertical;
        var frame = state.Selected ? TextRole.FrameFocus : TextRole.Muted;
        var text = TextOf(part, state);
        var indent = part is CardGroupPart.Middle or CardGroupPart.Last ? ChildIndent : 0;

        spans.Add(new StyledSpan(side, frame));

        if (indent > 0)
        {
            spans.Add(new StyledSpan(new string(' ', indent), text));
        }

        AddBoxText(spans, issue, row, state, width - 2 - indent, text);
        spans.Add(new StyledSpan(side, frame));
    }

    private static TextRole TextOf(CardGroupPart part, CardState state)
    {
        if (state.Selected)
        {
            return TextRole.Selected;
        }

        return part == CardGroupPart.First ? TextRole.Strong : TextRole.Body;
    }

    /// <summary>
    /// The text between the side borders: a summary line, a blank row, a row of label chips, or the key row with the
    /// key, type word, counter, priority and assignee chip.
    /// </summary>
    private static void AddBoxText(
        List<StyledSpan> spans,
        IssueSummary issue,
        CardRow row,
        CardState state,
        int room,
        TextRole text)
    {
        var inner = room - 2;

        if (row.Kind == CardRowKind.Key)
        {
            // The cell before the key is the set mark: `*` for a marked card (never colour alone).
            var star = state.Marked ? MarkedMark : Blank;
            var starRole = state.Marked && !state.Selected ? TextRole.Strong : text;

            spans.Add(new StyledSpan(star, starRole));
            spans.AddRange(CardRows.Bottom(issue, state.Open, inner, text));
            spans.Add(new StyledSpan(Blank, text));

            return;
        }

        spans.Add(new StyledSpan(Blank, text));

        if (row.Kind == CardRowKind.Labels)
        {
            var labels = CardLabelLayout.Rows(issue.Labels, inner);

            spans.AddRange(CardRows.Labels(labels[Math.Min(row.Index, labels.Count - 1)], inner));
        }
        else
        {
            spans.Add(new StyledSpan(TextWidth.Pad(SummaryLine(issue, row, inner), inner), text));
        }

        spans.Add(new StyledSpan(Blank, text));
    }

    private static string SummaryLine(IssueSummary issue, CardRow row, int inner)
    {
        if (row.Kind != CardRowKind.Summary)
        {
            return string.Empty;
        }

        var lines = CardText.TwoLines(issue.Summary, inner);

        return row.Index == 0 ? lines.First : lines.Second;
    }

    /// <summary>
    /// The small header in the top border of a card whose parent is not shown above it in the same column:
    /// <c> ↑ GTM-9 Parent summary </c>, cut to the border; empty for an issue without a parent.
    /// </summary>
    private static string ParentLabel(IssueSummary issue, int room, char fill)
    {
        if (issue.Parent is not { } parent || room < 6)
        {
            return string.Empty;
        }

        var summary = string.IsNullOrWhiteSpace(parent.Summary) ? string.Empty : $" {parent.Summary}";
        var text = CardText.Shorten($"{fill}{Blank}{ParentMark} {parent.Key}{summary}", room - 2);

        return $"{text}{Blank}";
    }

    private static string CompactLine(IssueSummary issue, bool selected, bool marked, bool nested, bool more, int width)
    {
        return CardText.Compact(issue, selected, marked, nested, more, width);
    }
}
