using Geeratya.Core.Boards;
using Geeratya.Core.Detail;
using Geeratya.Core.Lists;
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

    /// <summary>The text between the side borders; the type name on the key line is coloured by type.</summary>
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
        if (within != 1 && issue.Subtasks.Count > 0 && !issue.IsListed)
        {
            var line = within == FirstSummaryLine
                ? CardText.Shorten(issue.Summary, room - 2)
                : SubtaskRow(issue.Subtasks, open, room - 2);

            spans.Add(new StyledSpan(TextWidth.Pad($" {line} ", room), text));

            return;
        }

        if (within != 1)
        {
            var lines = CardText.TwoLines(issue.Summary, room - 2);

            spans.Add(new StyledSpan(
                TextWidth.Pad($" {(within == FirstSummaryLine ? lines.First : lines.Second)} ", room),
                text));

            return;
        }

        var (label, remainder) = KeyLine(issue, room - 2);
        var tail = TextWidth.Pad($"{remainder} ", room - 1 - TextWidth.Of(label));

        // The cell before the type name is the set mark: `*` for a marked card (never colour alone).
        var star = marked ? MarkedMark : Blank;
        var starRole = marked && !selected ? TextRole.Strong : text;

        spans.Add(new StyledSpan(star, starRole));
        spans.Add(new StyledSpan(label, IssueTypeRole.For(issue.IssueType)));

        var trimmed = tail.TrimEnd();
        var cut = issue.IsListed && trimmed.EndsWith(']') ? trimmed.LastIndexOf('[') : -1;

        if (cut < 0)
        {
            spans.Add(new StyledSpan(tail, text));

            return;
        }

        var chip = trimmed[cut..];

        // A listed sub-task shows its status as a bracketed chip in the colour of its category (#645).
        spans.Add(new StyledSpan(tail[..cut], text));
        spans.Add(new StyledSpan(chip, StatusRoles.Of(issue.Status)));
        spans.Add(new StyledSpan(tail[(cut + chip.Length)..], text));
    }

    /// <summary>The status of a listed sub-task as <c>[Name]</c>: readable without colour too.</summary>
    private static string StatusChip(IssueStatus status)
    {
        return $"[{status.Name}]";
    }

    /// <summary>
    /// The sub-task row of a card, the second summary line: the fold mark, <c>Sub-tasks done/total</c> and a bar of
    /// <see cref="SubtaskBarCells"/> cells; the word and then the bar go first when the card is narrow (#520).
    /// </summary>
    private static string SubtaskRow(IReadOnlyList<IssueSubtask> subtasks, bool open, int room)
    {
        var done = subtasks.Count(subtask => subtask.IsDone);
        var mark = open ? "▾" : "▸";
        var count = $"{done}/{subtasks.Count}";
        var filled = done * SubtaskBarCells / subtasks.Count;
        var bar = $"{new string('━', filled)}{new string('─', SubtaskBarCells - filled)}";
        var full = $"{mark} Sub-tasks {count} {bar}";

        if (TextWidth.Of(full) <= room)
        {
            return full;
        }

        var narrow = $"{mark} {count} {bar}";

        return TextWidth.Of(narrow) <= room ? narrow : CardText.Shorten($"{mark} {count}", room);
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

    /// <summary>
    /// The key line: type name and key, with the assignee's initials at the right edge when there is room. The type
    /// name wins over the initials: it is cut down to <see cref="MinTypeName"/> cells, then the initials go, then the
    /// name is replaced by the one-cell marker.
    /// </summary>
    private static (string Label, string Remainder) KeyLine(IssueSummary issue, int width)
    {
        var initials = issue.Assignee is null ? string.Empty : AssigneeInitials.Of(issue.Assignee.DisplayName);

        if (issue.IsListed)
        {
            // A long status is cut to keep the chip before the type name gives way (#645).
            var chipRoom = width - TextWidth.Of(issue.Key) - 1 - MinTypeName - 1;
            var chip = StatusChip(issue.Status);

            initials = TextWidth.Of(chip) <= chipRoom || chipRoom < 5
                ? chip
                : $"[{CardText.Shorten(issue.Status.Name, chipRoom - 2)}]";
        }

        if (issue.Estimate is not null)
        {
            initials = $"{SprintHeaderText.Estimate(issue.Estimate)}p {initials}".TrimEnd();
        }

        var keyRoom = width - TextWidth.Of(issue.Key) - 1;

        if (initials.Length > 0 && keyRoom - TextWidth.Of(initials) - 1 < MinTypeName)
        {
            initials = string.Empty;
        }

        var nameRoom = initials.Length > 0 ? keyRoom - TextWidth.Of(initials) - 1 : keyRoom;
        var label = nameRoom >= MinTypeName && !string.IsNullOrWhiteSpace(issue.IssueType.Name)
            ? CardText.Shorten(issue.IssueType.Name, nameRoom)
            : IssueTypeMarker.For(issue.IssueType).ToString();
        var key = $" {issue.Key}";

        if (initials.Length == 0)
        {
            return (label, TextWidth.Fit(key, Math.Max(0, width - TextWidth.Of(label))));
        }

        return (label, $"{TextWidth.Pad(key, width - TextWidth.Of(initials) - 1 - TextWidth.Of(label))} {initials}");
    }
}
