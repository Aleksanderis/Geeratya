using Geeratya.Core.Boards;
using Geeratya.Core.Detail;
using Geeratya.Core.Lists;
using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The label row and the bottom row of a board card as spans (<c>docs/ux-screens.md</c>, "Board card"). Pure: the same
/// issue and width always give the same cells, every row exactly as wide as asked.
/// </summary>
public static class CardRows
{
    private const int MinTypeName = 4;
    private const int ChipCells = 4;
    private const int InitialsCells = 2;
    private const int GlyphCells = 2;
    private const string UnassignedText = "--";
    private const int ChipBrackets = 2;
    private const int MinStatusChip = 5;
    private const string Gap = "  ";
    private const string Blank = " ";


    /// <summary>
    /// One row of label chips as spans: <c>[label]</c> each in the stable colour of its text, then <c>+N</c> on the last
    /// row when labels did not fit; padded to <paramref name="width"/>.
    /// </summary>
    public static List<StyledSpan> Labels(CardLabelRow row, int width)
    {
        var spans = new List<StyledSpan>();
        var used = 0;

        foreach (var chip in row.Chips)
        {
            if (used > 0)
            {
                spans.Add(new StyledSpan(Blank, TextRole.Body));
                used++;
            }

            spans.Add(new StyledSpan(chip.Text, LabelChips.For(chip.Label)));
            used += TextWidth.Of(chip.Text);
        }

        if (row.Hidden > 0)
        {
            var rest = $"{(used > 0 ? Blank : string.Empty)}+{row.Hidden}";

            spans.Add(new StyledSpan(rest, TextRole.Muted));
            used += TextWidth.Of(rest);
        }

        spans.Add(new StyledSpan(new string(' ', Math.Max(0, width - used)), TextRole.Body));

        return spans;
    }

    /// <summary>
    /// The bottom row: the key, two spaces, the issue type as a word in its type colour and the sub-task counter when
    /// there is room; at the right the priority glyph and the assignee chip (<c>[GR]</c>, <c>[--]</c> unassigned, the
    /// same width). A listed sub-task shows its status chip there instead. The type word wins over the right side,
    /// which gives way piece by piece (the priority glyph first, so the counter keeps its place), then the word shrinks
    /// to the one-cell marker.
    /// </summary>
    public static List<StyledSpan> Bottom(IssueSummary issue, bool open, int width, TextRole text)
    {
        var counter = Counter(issue, open);
        var counterCells = counter.Length == 0 ? 0 : 1 + TextWidth.Of(counter);
        var right = Right(issue, width - TextWidth.Of(issue.Key) - Gap.Length - MinTypeName - 1, counterCells);
        var rightWidth = right.Sum(static span => TextWidth.Of(span.Text));
        var left = width - (rightWidth > 0 ? rightWidth + 1 : 0);
        var key = TextWidth.Fit(issue.Key, left);
        var used = TextWidth.Of(key);
        var spans = new List<StyledSpan> { new(key, TextRole.Key) };
        var room = left - used - Gap.Length;

        if (room >= 1)
        {
            var name = TypeWord(issue, room);

            spans.Add(new StyledSpan(Gap, text));
            spans.Add(new StyledSpan(name, IssueTypeRole.For(issue.IssueType)));
            used += Gap.Length + TextWidth.Of(name);
        }

        if (counter.Length > 0 && left - used - 1 >= TextWidth.Of(counter))
        {
            spans.Add(new StyledSpan($" {counter}", text));
            used += 1 + TextWidth.Of(counter);
        }

        spans.Add(new StyledSpan(new string(' ', Math.Max(0, left - used)), text));

        if (rightWidth > 0)
        {
            spans.Add(new StyledSpan(Blank, text));
            spans.AddRange(right);
        }

        return spans;
    }

    private static string TypeWord(IssueSummary issue, int room)
    {
        if (room < MinTypeName || string.IsNullOrWhiteSpace(issue.IssueType.Name))
        {
            return IssueTypeMarker.For(issue.IssueType).ToString();
        }

        return CardText.Shorten(issue.IssueType.Name, room);
    }

    private static string Counter(IssueSummary issue, bool open)
    {
        if (issue.IsListed || issue.Subtasks.Count == 0)
        {
            return string.Empty;
        }

        var done = issue.Subtasks.Count(static subtask => subtask.IsDone);
        var mark = open ? "▾" : "▸";

        return $"{mark}{done}/{issue.Subtasks.Count}";
    }

    private static List<StyledSpan> Right(IssueSummary issue, int room, int counterCells)
    {
        var spans = new List<StyledSpan>();
        var estimate = issue.Estimate is null ? string.Empty : $"{SprintHeaderText.Estimate(issue.Estimate)}p ";

        if (issue.IsListed)
        {
            AddStatus(spans, issue, estimate, room);

            return spans;
        }

        if (room < ChipCells)
        {
            return spans;
        }

        var spare = room - ChipCells;
        var glyph = PriorityGlyph(issue.Priority);

        if (glyph.Length > 0 && spare >= GlyphCells + counterCells)
        {
            spans.Add(new StyledSpan(glyph, PriorityRole(issue.Priority)));
            spans.Add(new StyledSpan(Blank, TextRole.Body));
            spare -= GlyphCells;
        }

        var unassigned = issue.Assignee is null || AssigneeInitials.Of(issue.Assignee.DisplayName).Length == 0;
        var role = unassigned ? TextRole.UnassignedChip : TextRole.InitialsChip;

        spans.Add(new StyledSpan($"[{TextWidth.Pad(InitialsOf(issue), InitialsCells)}]", role));

        if (estimate.Length > 0 && spare >= TextWidth.Of(estimate) + counterCells)
        {
            spans.Insert(0, new StyledSpan(estimate, TextRole.Muted));
        }

        return spans;
    }

    private static string InitialsOf(IssueSummary issue)
    {
        var initials = issue.Assignee is null ? string.Empty : AssigneeInitials.Of(issue.Assignee.DisplayName);

        if (initials.Length == 0)
        {
            return UnassignedText;
        }

        return TextWidth.Of(initials) > InitialsCells ? TextWidth.Fit(initials, InitialsCells) : initials;
    }

    private static void AddStatus(List<StyledSpan> spans, IssueSummary issue, string estimate, int room)
    {
        var chip = $"[{issue.Status.Name}]";

        if (TextWidth.Of(chip) > room)
        {
            // A long status is cut so the chip stays before the type name gives way (#645).
            var cut = CardText.Shorten(issue.Status.Name, room - ChipBrackets);

            chip = room >= MinStatusChip ? $"[{cut}]" : string.Empty;
        }

        if (chip.Length == 0)
        {
            return;
        }

        if (estimate.Length > 0 && room - TextWidth.Of(chip) >= TextWidth.Of(estimate))
        {
            spans.Add(new StyledSpan(estimate, TextRole.Muted));
        }

        spans.Add(new StyledSpan(chip, StatusRoles.Of(issue.Status)));
    }

    private static string PriorityGlyph(string? priority)
    {
        var glyphs = Glyphs.For(GlyphSet.Unicode);

        return PriorityTierOf.Name(priority) switch
        {
            PriorityTier.High => glyphs.PriorityHigh,
            PriorityTier.Medium => glyphs.PriorityMedium,
            PriorityTier.Low => glyphs.PriorityLow,
            _ => string.Empty,
        };
    }

    private static TextRole PriorityRole(string? priority)
    {
        return PriorityTierOf.Name(priority) switch
        {
            PriorityTier.High => TextRole.PriorityHigh,
            PriorityTier.Medium => TextRole.PriorityMedium,
            _ => TextRole.PriorityLow,
        };
    }
}
