using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// The header facts of the shown issue, from the loaded issue or else from the list row it was opened from.
/// </summary>
internal sealed record HeaderFields(
    string TypeName,
    string Summary,
    IssueStatus? Status,
    string? Priority,
    string? Assignee,
    string? Reporter,
    IReadOnlyList<string> Labels,
    string? Parent,
    DateTimeOffset? Updated,
    string? Epic = null,
    string? Sprint = null,
    string? Estimate = null,
    bool Known = true)
{
    private static readonly HeaderFields _empty =
        new(string.Empty, string.Empty, null, null, null, null, [], null, null, Known: false);

    public static HeaderFields Of(DetailState detail)
    {
        if (detail.Issue is { } issue)
        {
            return FromIssue(issue);
        }

        return detail.Row is { } row ? FromRow(row) : _empty;
    }

    private static HeaderFields FromIssue(IssueDetail issue)
    {
        var parent = issue.Parent is { } stub ? $"{stub.Key} {stub.Summary}".TrimEnd() : null;
        var isEpic = issue.Epic is not null;

        return new HeaderFields(
            issue.IssueType.Name,
            issue.Summary,
            issue.Status,
            issue.Priority,
            issue.Assignee?.DisplayName,
            issue.Reporter?.DisplayName,
            issue.Labels,
            isEpic ? null : parent,
            issue.Updated,
            isEpic ? parent : null,
            SprintText(issue.Sprints),
            issue.Estimate is { } estimate ? EstimateText.Format(estimate) : null);
    }

    private static HeaderFields FromRow(IssueSummary row)
    {
        var parent = row.Parent is { } stub ? $"{stub.Key} {stub.Summary}".TrimEnd() : null;

        return new HeaderFields(
            row.IssueType.Name,
            row.Summary,
            row.Status,
            row.Priority,
            row.Assignee?.DisplayName,
            null,
            row.Labels,
            row.Epic is null ? parent : null,
            row.Updated,
            row.Epic is null ? null : parent,
            SprintText(row.Sprints),
            row.Estimate is { } estimate ? EstimateText.Format(estimate) : null);
    }

    /// <summary>The current sprint with its state, then the earlier sprints the issue was carried through.</summary>
    private static string? SprintText(IReadOnlyList<IssueSprint> sprints)
    {
        if (IssueSprint.Current(sprints) is not { } current)
        {
            return null;
        }

        var text = $"{current.Name} ({current.State.ToString().ToLowerInvariant()})";
        var earlier = sprints
            .Where(sprint => sprint.Id != current.Id && sprint.State == SprintState.Closed)
            .Select(sprint => sprint.Name)
            .ToList();

        return earlier.Count == 0 ? text : $"{text}; earlier: {string.Join(", ", earlier)}";
    }
}
