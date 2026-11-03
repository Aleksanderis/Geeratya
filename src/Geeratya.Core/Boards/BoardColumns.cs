using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Boards;

/// <summary>
/// Sorts issues into the columns of a board. Column order, names and membership come from the configuration only,
/// never from fixed names: the first column is not necessarily the backlog.
/// </summary>
public static class BoardColumns
{
    /// <summary>The name of the trailing column that holds issues whose status is in no board column.</summary>
    public const string UnmappedName = "Unmapped";

    private const string AllIssuesName = "Issues";

    /// <summary>
    /// The columns the columns layout draws: the board's own columns in board order, then <see cref="UnmappedName"/>
    /// only when an issue needs it, so no loaded issue is hidden. Without a configuration (not loaded yet) every row
    /// sits in one column.
    /// </summary>
    public static IReadOnlyList<BoardColumnGroup> Visible(
        BoardConfiguration? configuration,
        IReadOnlyList<IssueSummary> rows)
    {
        if (configuration is null)
        {
            return rows.Count == 0 ? [] : [Arrange(AllIssuesName, rows)];
        }

        var groups = Group(configuration, rows);
        var columns = HidesBacklogColumn(configuration) ? groups.Columns.Skip(1).ToList() : groups.Columns;

        if (groups.Unmapped.Count == 0)
        {
            return columns;
        }

        return [.. columns, Arrange(UnmappedName, groups.Unmapped)];
    }

    /// <summary>
    /// True when Jira does not show the first column on the board: a company-managed Kanban board whose first column
    /// is the Backlog column (its issues live in the Backlog view). The configuration has no flag for the backlog
    /// feature, so the board kind and the name decide; a team-managed board keeps its Backlog column (it is a normal
    /// column there, third on the dev board) and so does a Kanban board whose first column has another name.
    /// </summary>
    public static bool HidesBacklogColumn(BoardConfiguration configuration)
    {
        return configuration is { Type: BoardType.Kanban, Columns: [{ Name: var first }, ..] }
            && string.Equals(first, "Backlog", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Groups <paramref name="rows"/> by status id into the columns of <paramref name="configuration"/>. The order
    /// of the rows is kept inside a column; a status listed in two columns belongs to the first.
    /// </summary>
    public static BoardColumnGroups Group(BoardConfiguration configuration, IReadOnlyList<IssueSummary> rows)
    {
        var columnOfStatus = new Dictionary<string, int>();

        for (var index = 0; index < configuration.Columns.Count; index++)
        {
            foreach (var statusId in configuration.Columns[index].StatusIds)
            {
                columnOfStatus.TryAdd(statusId, index);
            }
        }

        var buckets = new List<IssueSummary>[configuration.Columns.Count];

        for (var index = 0; index < buckets.Length; index++)
        {
            buckets[index] = [];
        }

        var unmapped = new List<IssueSummary>();

        foreach (var row in rows)
        {
            if (columnOfStatus.TryGetValue(row.Status.Id, out var column))
            {
                buckets[column].Add(row);
            }
            else
            {
                unmapped.Add(row);
            }
        }

        var groups = new BoardColumnGroup[buckets.Length];

        for (var index = 0; index < groups.Length; index++)
        {
            groups[index] = Arrange(configuration.Columns[index].Name, buckets[index]);
        }

        return new BoardColumnGroups(groups, unmapped);
    }

    /// <summary>
    /// Builds a column from <paramref name="rows"/>: a child whose parent is in the same column moves to just after
    /// the parent (after the children already placed there) and is marked nested; every other row keeps its place.
    /// </summary>
    public static BoardColumnGroup Arrange(string name, IReadOnlyList<IssueSummary> rows)
    {
        if (!rows.Any(row => row.Parent is not null))
        {
            return new BoardColumnGroup(name, rows);
        }

        var keys = new HashSet<string>(rows.Select(row => row.Key), StringComparer.Ordinal);
        var children = new Dictionary<string, List<IssueSummary>>(StringComparer.Ordinal);

        bool UnderParent(IssueSummary row)
        {
            return row.Parent is { } parent && parent.Key != row.Key && keys.Contains(parent.Key);
        }

        foreach (var row in rows.Where(UnderParent))
        {
            if (!children.TryGetValue(row.Parent!.Key, out var list))
            {
                children[row.Parent.Key] = list = [];
            }

            list.Add(row);
        }

        if (children.Count == 0)
        {
            return new BoardColumnGroup(name, rows);
        }

        var ordered = new List<IssueSummary>(rows.Count);
        var nested = new List<bool>(rows.Count);
        var placed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (!UnderParent(row))
            {
                Place(row, false, children, ordered, nested, placed);
            }
        }

        // A parent loop (not possible in Jira) would leave rows unplaced; they are appended in load order.
        foreach (var row in rows)
        {
            Place(row, false, children, ordered, nested, placed);
        }

        return new BoardColumnGroup(name, ordered, nested);
    }

    private static void Place(
        IssueSummary row,
        bool isNested,
        Dictionary<string, List<IssueSummary>> children,
        List<IssueSummary> ordered,
        List<bool> nested,
        HashSet<string> placed)
    {
        if (!placed.Add(row.Key))
        {
            return;
        }

        ordered.Add(row);
        nested.Add(isNested);

        if (children.TryGetValue(row.Key, out var list))
        {
            foreach (var child in list)
            {
                Place(child, true, children, ordered, nested, placed);
            }
        }
    }
}
