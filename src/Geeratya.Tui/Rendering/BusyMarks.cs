using Geeratya.Core.Boards;
using Geeratya.Core.State;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Marks the issues that a running write is changing: the summary starts with the ellipsis glyph until Jira has
/// answered (<c>docs/ux-workflows.md</c>, "Write something"). Only the shown text is changed, never the state.
/// </summary>
public static class BusyMarks
{
    /// <summary>The keys under a running operation, empty when none runs.</summary>
    public static HashSet<string> Keys(AppState state)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in state.Session.Operations)
        {
            keys.UnionWith(operation.Keys);
        }

        return keys;
    }

    public static IssueSummary Mark(IssueSummary row, HashSet<string> busy, Glyphs glyphs)
    {
        return busy.Contains(row.Key) ? row with { Summary = $"{glyphs.Ellipsis} {row.Summary}" } : row;
    }

    public static IReadOnlyList<BoardColumnGroup> Mark(
        IReadOnlyList<BoardColumnGroup> columns,
        HashSet<string> busy,
        Glyphs glyphs)
    {
        if (busy.Count == 0)
        {
            return columns;
        }

        return [.. columns.Select(column => MarkColumn(column, busy, glyphs))];
    }

    private static BoardColumnGroup MarkColumn(BoardColumnGroup column, HashSet<string> busy, Glyphs glyphs)
    {
        if (!column.Rows.Any(row => busy.Contains(row.Key)))
        {
            return column;
        }

        return column with { Rows = [.. column.Rows.Select(row => Mark(row, busy, glyphs))] };
    }
}
