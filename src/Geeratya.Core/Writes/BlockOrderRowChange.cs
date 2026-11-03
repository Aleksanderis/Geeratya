using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// A block of rows moved next to another row: <paramref name="Keys"/> now sit together, in this order, directly
/// before or after <paramref name="ReferenceKey"/>. Only tabs whose order is Jira's rank are patched. The rows
/// themselves do not change. Undoing puts the block members back into the places they had in
/// <paramref name="OriginalOrder"/>.
/// </summary>
/// <param name="Keys">The moved issues in block order; the first one carries the change.</param>
/// <param name="ReferenceKey">The issue the block was placed next to.</param>
/// <param name="Before">The block sits before the reference issue, else after it.</param>
/// <param name="OriginalOrder">The keys of the view before the move.</param>
public sealed record BlockOrderRowChange(
    IReadOnlyList<string> Keys,
    string ReferenceKey,
    bool Before,
    IReadOnlyList<string> OriginalOrder) : RowChange(Keys[0])
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
        if (!tab.Definition.Source.IsBoardBased() || IsPlaced(tab.Rows) || IndexOf(tab.Rows, ReferenceKey) < 0)
        {
            return tab.Rows;
        }

        var block = Block(tab.Rows);

        if (block.Count == 0)
        {
            return tab.Rows;
        }

        var rest = tab.Rows.Where(row => !IsMember(row.Key)).ToList();
        var at = IndexOf(rest, ReferenceKey);

        rest.InsertRange(Before ? at : at + 1, block);

        return [.. rest];
    }

    public override IReadOnlyList<IssueSummary> RevertOrder(TabState tab)
    {
        if (!tab.Definition.Source.IsBoardBased() || !IsPlaced(tab.Rows))
        {
            return tab.Rows;
        }

        var slots = new List<int>();
        var known = new List<IssueSummary>();

        for (var index = 0; index < tab.Rows.Count; index++)
        {
            if (IndexOf(OriginalOrder, tab.Rows[index].Key) >= 0)
            {
                slots.Add(index);
                known.Add(tab.Rows[index]);
            }
        }

        var copy = new List<IssueSummary>(tab.Rows);

        known.Sort((left, right) => IndexOf(OriginalOrder, left.Key).CompareTo(IndexOf(OriginalOrder, right.Key)));

        for (var index = 0; index < slots.Count; index++)
        {
            copy[slots[index]] = known[index];
        }

        return copy;
    }

    /// <summary>True when the block is already together, in order, next to the reference issue.</summary>
    private bool IsPlaced(IReadOnlyList<IssueSummary> rows)
    {
        var reference = IndexOf(rows, ReferenceKey);

        if (reference < 0)
        {
            return false;
        }

        var start = Before ? reference - Keys.Count : reference + 1;

        if (start < 0 || start + Keys.Count > rows.Count)
        {
            return false;
        }

        for (var index = 0; index < Keys.Count; index++)
        {
            if (!string.Equals(rows[start + index].Key, Keys[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private List<IssueSummary> Block(IReadOnlyList<IssueSummary> rows)
    {
        var block = new List<IssueSummary>();

        foreach (var key in Keys)
        {
            var index = IndexOf(rows, key);

            if (index >= 0)
            {
                block.Add(rows[index]);
            }
        }

        return block;
    }

    private bool IsMember(string key)
    {
        return Keys.Contains(key, StringComparer.OrdinalIgnoreCase);
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

    private static int IndexOf(IReadOnlyList<string> keys, string key)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            if (string.Equals(keys[index], key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
