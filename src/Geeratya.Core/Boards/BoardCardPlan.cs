using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Boards;

/// <summary>
/// How the three text rows of a full card are shared (<c>docs/ux-screens.md</c>, "Board card"): the bottom row is
/// always the key row, a card with labels shows one summary line and the label row, a card without labels shows
/// two summary lines. The renderer and the flash labeler agree on it through here.
/// </summary>
public static class BoardCardPlan
{
    /// <summary>The summary lines the card shows: two, or one when the label row takes the second.</summary>
    public static int SummaryLines(IssueSummary issue)
    {
        return HasLabelRow(issue) ? 1 : 2;
    }

    /// <summary>Whether the card has a label row.</summary>
    public static bool HasLabelRow(IssueSummary issue)
    {
        return issue.Labels.Count > 0;
    }
}
