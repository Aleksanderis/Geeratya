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
            return rows.Count == 0 ? [] : [new BoardColumnGroup(AllIssuesName, rows)];
        }

        var groups = Group(configuration, rows);

        if (groups.Unmapped.Count == 0)
        {
            return groups.Columns;
        }

        return [.. groups.Columns, new BoardColumnGroup(UnmappedName, groups.Unmapped)];
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
            groups[index] = new BoardColumnGroup(configuration.Columns[index].Name, buckets[index]);
        }

        return new BoardColumnGroups(groups, unmapped);
    }
}
