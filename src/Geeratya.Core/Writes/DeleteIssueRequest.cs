using System.Globalization;
using System.Text;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// Delete <paramref name="Key"/>. Jira has no undo, so the confirm names the key, the title and the sub-tasks that
/// go with it. Not optimistic: the row disappears only after Jira answered (a refusal for permission or sub-tasks
/// must not make a row vanish and come back).
/// </summary>
/// <param name="Key">The issue.</param>
/// <param name="Summary">Its title, for the texts.</param>
/// <param name="SubtaskCount">The sub-tasks found when the question was asked; they are deleted with the issue.</param>
/// <param name="SubtasksMore">There are more sub-tasks than <see cref="SubtaskLimit"/> counted.</param>
public sealed record DeleteIssueRequest(string Key, string Summary, int SubtaskCount, bool SubtasksMore = false)
    : WriteRequest
{
    /// <summary>The most sub-tasks the question counts; more are reported as "more than".</summary>
    public const int SubtaskLimit = IssueSearchQuery.DefaultPageSize;

    private const int SummaryLimit = 40;
    private const string Ellipsis = "…";

    /// <summary>Whether the delete asks Jira to delete the sub-tasks too (<c>deleteSubtasks=true</c>).</summary>
    public bool DeleteSubtasks => SubtaskCount > 0;

    public override OperationKind Kind => OperationKind.DeleteIssue;

    public override IReadOnlyList<string> Keys => [Key];

    public override bool RemovesIssues => true;

    public override string BusyText => "deleting";

    public override string StartedText => $"Deleting {Key}";

    public override string DoneText => $"Deleted {Key}{WithSubtasksText}";

    /// <summary>
    /// Jira answers 404 for a deleted issue and for one the user cannot see alike, so the check after a lost reply
    /// cannot tell them apart.
    /// </summary>
    public override string VerifiedDoneText =>
        $"{Key}: probably deleted, or hidden from you";

    public override string CheckingText => $"{Key}: not sure the delete worked, checking";

    public override string NotAppliedText => $"{Key}: the delete was not applied";

    public override string ActionText => $"delete {Key}";

    /// <summary>
    /// The question of the confirm. What matters comes first (the key, the sub-tasks, that Jira cannot undo it) and
    /// the title last, because a narrow terminal cuts the end of the line.
    /// </summary>
    public string ConfirmText
    {
        get
        {
            var title = Shorten(Summary);
            var suffix = title.Length == 0 ? string.Empty : $" \"{title}\"";

            return $"Delete {Key}{WithSubtasksText}{HiddenText}? Jira cannot undo this.{suffix}";
        }
    }

    private string WithSubtasksText
    {
        get
        {
            if (SubtaskCount == 0)
            {
                return string.Empty;
            }

            var count = SubtaskCount.ToString(CultureInfo.InvariantCulture);
            var number = SubtasksMore ? $"more than {count}" : count;
            var noun = SubtaskCount == 1 && !SubtasksMore ? "sub-task" : "sub-tasks";

            return $" and its {number} {noun}";
        }
    }

    // Sent with deleteSubtasks=true, which also removes sub-tasks the user cannot see; the count cannot include them.
    private string HiddenText => SubtaskCount == 0 ? string.Empty : " (and any hidden from you)";

    // ReSharper disable once ReturnTypeCanBeNotNullable (the base says null for writes that are not optimistic)
    public override RowChange? Plan(IssueSummary row)
    {
        return null;
    }

    public override bool IsShownBy(IssueSummary row)
    {
        return false;
    }

    private static string Shorten(string summary)
    {
        var builder = new StringBuilder(summary.Length);

        foreach (var character in summary)
        {
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        var clean = builder.ToString().Trim();

        return clean.Length <= SummaryLimit ? clean : $"{clean[..(SummaryLimit - 1)].TrimEnd()}{Ellipsis}";
    }
}
