using Geeratya.Core.Boards;
using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Tui.Rendering;

public sealed partial class ColumnsBodyRenderer
{
    /// <summary>One row of the bordered card, in a margin of one cell on each side of the column.</summary>
    private static void AddBoxRow(
        List<StyledSpan> spans,
        IssueSummary issue,
        int within,
        bool selected,
        bool marked,
        bool open,
        GroupPart part,
        int inner,
        bool heavyBottom)
    {
        var width = inner - (2 * Margin);

        if (width < MinimumBoxWidth)
        {
            spans.Add(new StyledSpan(new string(' ', inner), TextRole.Body));

            return;
        }

        var frame = selected ? TextRole.FrameFocus : TextRole.Muted;
        var text = selected ? TextRole.Selected : TextRole.Body;

        spans.Add(new StyledSpan(new string(' ', Margin), TextRole.Body));

        if (part != GroupPart.None)
        {
            AddGroupedRow(spans, issue, within, selected, marked, open, part, width, heavyBottom);
        }
        else if (within == 0 || within == BoardCards.FullRows - 1)
        {
            var heavy = selected;
            var (left, right) = (within == 0, heavy) switch
            {
                (true, true) => ("┏", "┓"),
                (true, false) => ("┌", "┐"),
                (false, true) => ("┗", "┛"),
                _ => ("└", "┘"),
            };
            var fill = heavy ? '━' : HorizontalChar;
            var label = within == 0 ? ParentLabel(issue, width - 2, fill) : string.Empty;
            var bar = label + new string(fill, width - 2 - TextWidth.Of(label));

            spans.Add(new StyledSpan($"{left}{bar}{right}", frame));
        }
        else
        {
            var side = selected ? "┃" : Vertical;

            spans.Add(new StyledSpan(side, frame));
            AddBoxText(spans, issue, within, selected, marked, open, width - 2, text);
            spans.Add(new StyledSpan(side, frame));
        }

        spans.Add(new StyledSpan(Blank, TextRole.Body));
    }

    /// <summary>
    /// One row of a card inside the shared frame of a parent and its children. The parent is the top of the frame
    /// (top border, three text rows, divider); a child is three text rows, a blank row and a divider, the last
    /// child's divider being the bottom of the frame. The rows keep the five-row pitch of a card. A selected card
    /// has heavy sides and the <c>Selected</c> role on its text.
    /// </summary>
    private static void AddGroupedRow(
        List<StyledSpan> spans,
        IssueSummary issue,
        int within,
        bool selected,
        bool marked,
        bool open,
        GroupPart part,
        int width,
        bool heavyBottom)
    {
        var first = part == GroupPart.First;
        var textRows = first ? within is >= 1 and <= 3 : within <= 2;
        var blank = !first && within == 3;

        if (!textRows && !blank)
        {
            var top = first && within == 0;
            var heavy = top ? selected : heavyBottom;
            var (left, right) = (top, part == GroupPart.Last) switch
            {
                (true, _) => heavy ? ("┏", "┓") : ("┌", "┐"),
                (_, true) => heavy ? ("┗", "┛") : ("└", "┘"),
                _ => heavy ? ("┣", "┫") : ("├", "┤"),
            };
            var fill = heavy ? '━' : HorizontalChar;

            var label = first && within == 0 ? ParentLabel(issue, width - 2, fill) : string.Empty;
            var bar = label + new string(fill, width - 2 - TextWidth.Of(label));

            spans.Add(new StyledSpan($"{left}{bar}{right}", heavy ? TextRole.FrameFocus : TextRole.Muted));

            return;
        }

        var side = selected ? "┃" : Vertical;
        var frame = selected ? TextRole.FrameFocus : TextRole.Muted;
        var text = selected ? TextRole.Selected : first ? TextRole.Strong : TextRole.Body;

        spans.Add(new StyledSpan(side, frame));

        if (blank)
        {
            spans.Add(new StyledSpan(new string(' ', width - 2), text));
        }
        else
        {
            var indent = first ? 0 : ChildIndent;

            if (indent > 0)
            {
                spans.Add(new StyledSpan(new string(' ', indent), text));
            }

            AddBoxText(spans, issue, first ? within : within + 1, selected, marked, open, width - 2 - indent, text);
        }

        spans.Add(new StyledSpan(side, frame));
    }

    /// <summary>
    /// The text between the side borders, three rows: the summary (two lines, or one when a label row follows),
    /// the label row, and the bottom row with the key, type word, counter, priority and assignee chip.
    /// </summary>
    private static void AddBoxText(
        List<StyledSpan> spans,
        IssueSummary issue,
        int within,
        bool selected,
        bool marked,
        bool open,
        int room,
        TextRole text)
    {
        var inner = room - 2;

        if (within == BottomRow)
        {
            // The cell before the key is the set mark: `*` for a marked card (never colour alone).
            var star = marked ? MarkedMark : Blank;
            var starRole = marked && !selected ? TextRole.Strong : text;

            spans.Add(new StyledSpan(star, starRole));
            spans.AddRange(CardRows.Bottom(issue, open, inner, text));
            spans.Add(new StyledSpan(Blank, text));

            return;
        }

        spans.Add(new StyledSpan(Blank, text));

        if (within == LabelRow && BoardCardPlan.HasLabelRow(issue))
        {
            spans.AddRange(CardRows.Labels(issue.Labels, inner));
        }
        else
        {
            var lines = CardText.TwoLines(issue.Summary, inner);
            var line = within == FirstSummaryLine ? lines.First : lines.Second;

            if (within == FirstSummaryLine && BoardCardPlan.SummaryLines(issue) == 1 && lines.Second.Length > 0)
            {
                line = CardText.Shorten(issue.Summary.Trim(), inner);
            }

            spans.Add(new StyledSpan(TextWidth.Pad(line, inner), text));
        }

        spans.Add(new StyledSpan(Blank, text));
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
