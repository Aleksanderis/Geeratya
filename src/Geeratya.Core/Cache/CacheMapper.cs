using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Cache;

/// <summary>Converts between the Jira models and the cache file DTOs. Pure.</summary>
public static class CacheMapper
{
    /// <summary>The file format version written into every cache file.</summary>
    public const int Schema = 1;

    public static CachedViewFile ToFile(CacheKey key, CachedView view)
    {
        return new CachedViewFile(
            Schema,
            key.Source.ToString(),
            key.SourceValue,
            view.SavedAt,
            [.. view.Rows.Select(ToRow)],
            view.Board is null ? null : ToBoard(view.Board));
    }

    /// <summary>The key and view in <paramref name="file"/>; null when the schema or content is not readable.</summary>
    public static KeyedView? FromFile(CachedViewFile file)
    {
        if (file.Schema != Schema || !TryParse<ViewSourceKind>(file.Source, out var source) || !IsComplete(file))
        {
            return null;
        }

        var key = new CacheKey(source, file.SourceValue);
        var rows = file.Rows.Select(FromRow).ToArray();
        var board = file.Board is null ? null : FromBoard(file.Board);
        var view = new CachedView(rows, board, file.SavedAt);

        return new KeyedView(key, view);
    }

    private static CachedIssueRow ToRow(IssueSummary row)
    {
        return new CachedIssueRow(
            row.Id,
            row.Key,
            row.Summary,
            new CachedStatus(row.Status.Id, row.Status.Name, row.Status.CategoryKey),
            new CachedIssueType(
                row.IssueType.Id,
                row.IssueType.Name,
                row.IssueType.HierarchyLevel,
                row.IssueType.IsSubtask),
            row.Priority,
            row.Assignee is null ? null : new CachedPerson(row.Assignee.AccountId, row.Assignee.DisplayName),
            [.. row.Labels],
            row.Parent is null ? null : new CachedParent(row.Parent.Key, row.Parent.Summary),
            row.Updated,
            row.ProjectKey);
    }

    private static IssueSummary FromRow(CachedIssueRow row)
    {
        return new IssueSummary(
            row.Id,
            row.Key,
            row.Summary,
            new IssueStatus(row.Status.Id, row.Status.Name, row.Status.CategoryKey),
            new IssueTypeSummary(
                row.IssueType.Id,
                row.IssueType.Name,
                row.IssueType.HierarchyLevel,
                row.IssueType.IsSubtask),
            row.Priority,
            row.Assignee is null ? null : new IssueAssignee(row.Assignee.AccountId, row.Assignee.DisplayName),
            [.. row.Labels],
            row.Parent is null ? null : new IssueParent(row.Parent.Key, row.Parent.Summary),
            row.Updated,
            row.ProjectKey);
    }

    private static CachedBoard ToBoard(BoardConfiguration board)
    {
        return new CachedBoard(
            board.BoardId,
            board.Name,
            board.Type.ToString(),
            [.. board.Columns.Select(column => new CachedColumn(column.Name, [.. column.StatusIds]))],
            board.FilterId,
            board.SubQuery,
            board.RankFieldId,
            board.ConstraintType);
    }

    private static BoardConfiguration FromBoard(CachedBoard board)
    {
        var type = TryParse<BoardType>(board.Type, out var parsed) ? parsed : BoardType.Unknown;

        return new BoardConfiguration(
            board.BoardId,
            board.Name,
            type,
            [.. board.Columns.Select(column => new BoardColumn(column.Name, [.. column.StatusIds]))],
            board.FilterId,
            board.SubQuery,
            board.RankFieldId,
            board.ConstraintType);
    }

    // JSON can hold nulls where the DTOs promise values, so these checks are not redundant.
    // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    /// <summary>A file from disk can hold JSON nulls where the DTOs promise values; such a file is unusable.</summary>
    private static bool IsComplete(CachedViewFile file)
    {
        if (file.SourceValue is null || file.Rows is null || !file.Rows.All(IsComplete))
        {
            return false;
        }

        return file.Board is null || (file.Board.Columns is not null && file.Board.Columns.All(IsComplete));
    }

    private static bool IsComplete(CachedIssueRow row)
    {
        return row is { Status: not null, IssueType: not null, Labels: not null }
            && row.Id is not null
            && row.Key is not null
            && row.Summary is not null
            && row.ProjectKey is not null;
    }

    private static bool IsComplete(CachedColumn column)
    {
        return column.Name is not null && column.StatusIds is not null;
    }

    // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    /// <summary>Parses an enum member by name; numbers and unknown names are refused.</summary>
    private static bool TryParse<TEnum>(string? text, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;

        return text is not null
            && !char.IsDigit(text.FirstOrDefault())
            && Enum.TryParse(text, out value)
            && Enum.IsDefined(value);
    }
}
