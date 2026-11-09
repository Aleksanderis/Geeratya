using Geeratya.Core.Boards;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>
/// The table of an issue relation pane (sub-tasks, child issues, linked issues; <c>docs/internal/ux-screens.md</c>, "Sub-tasks
/// pane"): one row per issue as aligned columns, key, summary, status and assignee. The key column is as wide as the
/// longest key, the status column as wide as the longest status name, the summary takes what is left and is cut with
/// an ellipsis, and as the width shrinks the assignee loses its name, then its chip, then the status goes. Pure.
/// </summary>
internal static class RelationTable
{
    /// <summary>Summary columns below which the assignee and then the status give way.</summary>
    public const int MinSummary = 12;

    private const int Gap = 2;
    private const int MaxName = 24;
    private const string UnassignedName = "Unassigned";
    private const string UnassignedInitials = "--";

    /// <summary>
    /// The lines of the table for <paramref name="inner"/> columns; each line opens its row's target.
    /// </summary>
    public static void Add(PanelContent content, IReadOnlyList<RelationRow> rows, int inner)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var plan = Plan.For(rows, inner);

        foreach (var row in rows)
        {
            content.Add(new StyledLine(Spans(row, plan)), row.Target);
        }
    }

    private static List<StyledSpan> Spans(RelationRow row, Plan plan)
    {
        var spans = new List<StyledSpan>
        {
            new(Pad(DisplayText.Cut(TextClean.Of(row.Key), plan.KeyWidth), plan.KeyWidth), TextRole.Key),
            new(new string(' ', Gap), TextRole.Body),
        };
        var room = plan.SummaryWidth;

        if (row.Prefix.Length > 0 && room > 0)
        {
            var prefix = DisplayText.Cut($"{TextClean.Of(row.Prefix)} ", room);

            spans.Add(new StyledSpan(prefix, TextRole.Emphasis));
            room -= DisplayWidth.Of(prefix);
        }

        var summary = DisplayText.Cut(TextClean.Of(row.Summary), room);

        spans.Add(new StyledSpan(Pad(summary, room), TextRole.Body));

        if (plan.StatusWidth > 0)
        {
            var status = DisplayText.Cut(TextClean.Of(row.StatusName), plan.StatusWidth);

            spans.Add(new StyledSpan(new string(' ', Gap), TextRole.Body));
            spans.Add(new StyledSpan(Pad(status, plan.StatusWidth), row.StatusRole));
        }

        AddAssignee(spans, row, plan);

        return spans;
    }

    private static void AddAssignee(List<StyledSpan> spans, RelationRow row, Plan plan)
    {
        if (plan.ChipWidth == 0)
        {
            return;
        }

        spans.Add(new StyledSpan(new string(' ', Gap), TextRole.Body));

        if (!row.AssigneeKnown)
        {
            spans.Add(new StyledSpan(new string(' ', plan.AssigneeWidth), TextRole.Body));

            return;
        }

        var unassigned = string.IsNullOrWhiteSpace(row.Assignee);
        var initials = unassigned ? UnassignedInitials : AssigneeInitials.Of(row.Assignee!);
        var chip = unassigned ? $"[{Pad(initials, plan.ChipWidth - 2)}]" : PersonChips.Text(initials, plan.ChipWidth - 2);
        var chipRole = unassigned ? TextRole.UnassignedChip : PersonChips.For(row.Assignee!);

        spans.Add(new StyledSpan(chip, chipRole));

        if (plan.NameWidth > 0)
        {
            var name = unassigned ? UnassignedName : TextClean.Of(row.Assignee!);
            var role = unassigned ? TextRole.Muted : TextRole.Body;

            spans.Add(new StyledSpan(" ", TextRole.Body));
            spans.Add(new StyledSpan(Pad(DisplayText.Cut(name, plan.NameWidth), plan.NameWidth), role));
        }
    }

    private static string Pad(string text, int width)
    {
        return text + new string(' ', Math.Max(0, width - DisplayWidth.Of(text)));
    }

    /// <summary>The column widths for one table at one width.</summary>
    private sealed record Plan(
        int KeyWidth,
        int SummaryWidth,
        int StatusWidth,
        int ChipWidth,
        int NameWidth)
    {
        /// <summary>The width of the whole assignee cell: chip, space and name.</summary>
        public int AssigneeWidth => ChipWidth == 0 ? 0 : ChipWidth + (NameWidth > 0 ? 1 + NameWidth : 0);

        public static Plan For(IReadOnlyList<RelationRow> rows, int inner)
        {
            var key = Math.Min(rows.Max(static row => DisplayWidth.Of(TextClean.Of(row.Key))), Math.Max(1, inner));
            var status = rows.Max(static row => DisplayWidth.Of(TextClean.Of(row.StatusName)));
            var assigned = rows.Where(static row => row.AssigneeKnown).ToList();
            var chip = assigned.Count == 0 ? 0 : 2 + assigned.Max(static row => InitialsWidth(row));
            var name = assigned.Count == 0 ? 0 : Math.Min(MaxName, assigned.Max(static row => NameWidthOf(row)));
            var candidates = new[]
            {
                new Plan(key, 0, status, chip, name),
                new Plan(key, 0, status, chip, 0),
                new Plan(key, 0, status, 0, 0),
                new Plan(key, 0, 0, 0, 0),
            };

            foreach (var candidate in candidates)
            {
                var summary = inner - candidate.Fixed();

                if (summary >= MinSummary || (candidate.ChipWidth == 0 && candidate.StatusWidth == 0))
                {
                    return candidate with { SummaryWidth = Math.Max(0, summary) };
                }
            }

            return candidates[^1] with { SummaryWidth = Math.Max(0, inner - candidates[^1].Fixed()) };
        }

        private static int InitialsWidth(RelationRow row)
        {
            if (string.IsNullOrWhiteSpace(row.Assignee))
            {
                return UnassignedInitials.Length;
            }

            return DisplayWidth.Of(AssigneeInitials.Of(row.Assignee));
        }

        private static int NameWidthOf(RelationRow row)
        {
            if (string.IsNullOrWhiteSpace(row.Assignee))
            {
                return UnassignedName.Length;
            }

            return DisplayWidth.Of(TextClean.Of(row.Assignee));
        }

        /// <summary>The columns taken by everything but the summary, gaps included.</summary>
        private int Fixed()
        {
            var width = KeyWidth + Gap;

            if (StatusWidth > 0)
            {
                width += Gap + StatusWidth;
            }

            if (ChipWidth > 0)
            {
                width += Gap + AssigneeWidth;
            }

            return width;
        }
    }
}
