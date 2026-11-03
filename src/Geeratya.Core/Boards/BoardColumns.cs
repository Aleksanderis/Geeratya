using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Boards;

/// <summary>
/// Sorts issues into the columns of a board. Column order, names and membership come from the configuration only,
/// never from fixed names, except that a company-managed Kanban board's leading <see cref="BacklogName"/> column is
/// hidden (<see cref="HidesBacklogColumn"/>).
/// </summary>
public static class BoardColumns
{
    /// <summary>The name of the trailing column that holds issues whose status is in no board column.</summary>
    public const string UnmappedName = "Unmapped";

    /// <summary>The name of the Backlog column that Jira hides on a company-managed Kanban board.</summary>
    public const string BacklogName = "Backlog";

    private const string AllIssuesName = "Issues";

    /// <summary>The hierarchy level of a sub-task, as Jira reports it.</summary>
    private const int SubtaskLevel = -1;

    /// <summary>
    /// The columns the columns layout draws: the board's own columns in board order, then <see cref="UnmappedName"/>
    /// only when an issue needs it, so no loaded issue is hidden. Without a configuration (not loaded yet) every row
    /// sits in one column.
    /// </summary>
    public static IReadOnlyList<BoardColumnGroup> Visible(
        BoardConfiguration? configuration,
        IReadOnlyList<IssueSummary> rows,
        IReadOnlyList<string>? expanded = null)
    {
        if (configuration is null)
        {
            return rows.Count == 0 ? [] : [Arrange(AllIssuesName, rows, expanded, rows)];
        }

        var groups = Group(configuration, rows, expanded);
        var columns = HidesBacklogColumn(configuration) ? [.. groups.Columns.Skip(1)] : groups.Columns;

        if (groups.Unmapped.Count == 0)
        {
            return columns;
        }

        return [.. columns, Arrange(UnmappedName, groups.Unmapped, expanded, rows)];
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
            && string.Equals(first, BacklogName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Groups <paramref name="rows"/> by status id into the columns of <paramref name="configuration"/>. The order
    /// of the rows is kept inside a column; a status listed in two columns belongs to the first.
    /// </summary>
    public static BoardColumnGroups Group(
        BoardConfiguration configuration,
        IReadOnlyList<IssueSummary> rows,
        IReadOnlyList<string>? expanded = null)
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
            groups[index] = Arrange(configuration.Columns[index].Name, buckets[index], expanded, rows);
        }

        return new BoardColumnGroups(groups, unmapped);
    }

    /// <summary>
    /// Builds a column from <paramref name="rows"/>: a child whose parent is in the same column moves to just after
    /// the parent (after the children already placed there) and is marked nested; every other row keeps its place.
    /// </summary>
    public static BoardColumnGroup Arrange(string name, IReadOnlyList<IssueSummary> rows)
    {
        return Arrange(name, rows, null, rows);
    }

    /// <summary>
    /// Like <see cref="Arrange(string, IReadOnlyList{IssueSummary})"/>, and under each card of
    /// <paramref name="expanded"/> that lists sub-tasks, a nested row for every sub-task that is not an issue of the
    /// source (<paramref name="all"/>), so a board filter that leaves sub-tasks out still shows them (#520). A
    /// sub-task that is a card keeps its own card.
    /// </summary>
    public static BoardColumnGroup Arrange(
        string name,
        IReadOnlyList<IssueSummary> rows,
        IReadOnlyList<string>? expanded,
        IReadOnlyList<IssueSummary> all)
    {
        var arranged = ArrangeParents(name, rows);

        if (expanded is not { Count: > 0 } || !rows.Any(row => row.Subtasks.Count > 0))
        {
            return arranged;
        }

        return WithListedSubtasks(arranged, expanded, all);
    }

    private static BoardColumnGroup WithListedSubtasks(
        BoardColumnGroup group,
        IReadOnlyList<string> expanded,
        IReadOnlyList<IssueSummary> all)
    {
        var known = new HashSet<string>(all.Select(row => row.Key), StringComparer.Ordinal);
        var ordered = new List<IssueSummary>();
        var nested = new List<bool>();

        for (var index = 0; index < group.Rows.Count; index++)
        {
            var row = group.Rows[index];

            ordered.Add(row);
            nested.Add(group.IsNested(index));

            if (!expanded.Contains(row.Key) || group.IsNested(index))
            {
                continue;
            }

            // After the children already placed under this parent.
            var end = index + 1;

            while (group.IsNested(end))
            {
                ordered.Add(group.Rows[end]);
                nested.Add(true);
                end++;
            }

            foreach (var subtask in row.Subtasks.Where(subtask => !known.Contains(subtask.Key)))
            {
                ordered.Add(Listed(row, subtask));
                nested.Add(true);
            }

            index = end - 1;
        }

        return new BoardColumnGroup(group.Name, ordered, nested);
    }

    /// <summary>The row an expanded parent shows for one of its sub-tasks, built from the stub.</summary>
    public static IssueSummary Listed(IssueSummary parent, IssueSubtask subtask)
    {
        return new IssueSummary(
            string.Empty,
            subtask.Key,
            subtask.Summary,
            subtask.Status,
            new IssueTypeSummary(string.Empty, subtask.TypeName, SubtaskLevel, true),
            null,
            null,
            [],
            new IssueParent(parent.Key, parent.Summary),
            null,
            parent.ProjectKey)
        {
            IsListed = true,
        };
    }

    private static BoardColumnGroup ArrangeParents(string name, IReadOnlyList<IssueSummary> rows)
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
