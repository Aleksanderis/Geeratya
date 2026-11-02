using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// A row moved next to another one: <paramref name="Key"/> now sits directly before or after
/// <paramref name="ReferenceKey"/>. Only tabs whose order is Jira's rank (a board or a backlog) hold the issues in
/// that order, so only they are patched. The row itself does not change.
/// </summary>
/// <param name="Key">The moved issue.</param>
/// <param name="ReferenceKey">The issue it was placed next to.</param>
/// <param name="Before">The moved issue sits before the reference issue, else after it.</param>
/// <param name="WasAfter">The issue that preceded the moved one before the change, null when it was first.</param>
/// <param name="WasBefore">The issue that followed the moved one before the change, null when it was last.</param>
public sealed record OrderRowChange(
    string Key,
    string ReferenceKey,
    bool Before,
    string? WasAfter,
    string? WasBefore) : RowChange(Key)
{
    public override IssueSummary Patch(IssueSummary row)
    {
        return row;
    }

    public override IssueDetail Patch(IssueDetail detail)
    {
        return detail;
    }

    public override IssueSummary Revert(IssueSummary row)
    {
        return row;
    }

    public override IReadOnlyList<IssueSummary> PatchOrder(TabState tab)
    {
        if (!IsRankOrdered(tab))
        {
            return tab.Rows;
        }

        var from = IndexOf(tab.Rows, Key);
        var reference = IndexOf(tab.Rows, ReferenceKey);

        if (from < 0 || reference < 0 || IsPlaced(from, reference))
        {
            return tab.Rows;
        }

        return Move(tab.Rows, from, ReferenceKey, Before);
    }

    public override IReadOnlyList<IssueSummary> RevertOrder(TabState tab)
    {
        if (!IsRankOrdered(tab))
        {
            return tab.Rows;
        }

        var from = IndexOf(tab.Rows, Key);
        var reference = IndexOf(tab.Rows, ReferenceKey);

        if (from < 0 || reference < 0 || !IsPlaced(from, reference))
        {
            return tab.Rows;
        }

        if (WasAfter is not null && IndexOf(tab.Rows, WasAfter) >= 0)
        {
            return Move(tab.Rows, from, WasAfter, false);
        }

        if (WasBefore is not null && IndexOf(tab.Rows, WasBefore) >= 0)
        {
            return Move(tab.Rows, from, WasBefore, true);
        }

        return tab.Rows;
    }

    private bool IsPlaced(int from, int reference)
    {
        return Before ? from + 1 == reference : from == reference + 1;
    }

    private static bool IsRankOrdered(TabState tab)
    {
        return tab.Definition.Source is ViewSourceKind.Board or ViewSourceKind.Backlog;
    }

    private static IssueSummary[] Move(
        IReadOnlyList<IssueSummary> rows,
        int from,
        string referenceKey,
        bool before)
    {
        var moved = rows[from];
        var list = new List<IssueSummary>(rows);

        list.RemoveAt(from);

        var at = IndexOf(list, referenceKey);

        list.Insert(before ? at : at + 1, moved);

        return [.. list];
    }

    private static int IndexOf(IReadOnlyList<IssueSummary> rows, string key)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (string.Equals(rows[index].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
