namespace Geeratya.Core.Lists;

/// <summary>
/// Column widths of the list layout for a terminal width (<c>docs/internal/ux-screens.md</c>, "List layout"). A width of zero
/// means the column is dropped. Layout is data: the renderer only places text by these numbers.
/// </summary>
/// <param name="Key">Width of the key column.</param>
/// <param name="Summary">Width of the summary column (the remaining space).</param>
/// <param name="Status">Width of the status column: <see cref="FullStatus"/> or 1 (category glyph only).</param>
/// <param name="Assignee">Width of the assignee column, 0 when dropped.</param>
/// <param name="Sprint">Width of the optional sprint column, 0 when absent or dropped.</param>
/// <param name="Points">Width of the optional estimate column, 0 when absent or dropped.</param>
/// <param name="Epic">Width of the optional epic column, 0 when absent or dropped.</param>
public readonly record struct ListColumns(
    int Key,
    int Summary,
    int Status,
    int Assignee,
    int Sprint = 0,
    int Points = 0,
    int Epic = 0)
{
    /// <summary>Width of the sprint column.</summary>
    public const int SprintWidth = 16;

    /// <summary>Width of the estimate column.</summary>
    public const int PointsWidth = 5;

    /// <summary>Width of the epic column.</summary>
    public const int EpicWidth = 14;

    /// <summary>The summary column keeps at least this many cells; an optional column that would squeeze it is dropped.</summary>
    public const int MinSummaryWidth = 30;

    /// <summary>Selection mark and its gap in front of the key.</summary>
    public const int MarkWidth = 2;

    /// <summary>Width of the status column when it shows the name.</summary>
    public const int FullStatus = 14;

    /// <summary>Width of the assignee column.</summary>
    public const int AssigneeWidth = 16;

    /// <summary>The longest key column.</summary>
    public const int MaxKeyWidth = 12;

    /// <summary>The narrowest key column, the width of its title.</summary>
    public const int MinKeyWidth = 3;

    /// <summary>The first width at which the assignee column shows.</summary>
    public const int AssigneeMinimumWidth = 100;

    /// <summary>The first width at which the status column shows its name instead of a glyph.</summary>
    public const int StatusNameMinimumWidth = 56;

    /// <summary>Width of the type marker column.</summary>
    public const int MarkerWidth = 1;

    private const int GlyphWidth = 1;

    /// <summary>The columns for a terminal <paramref name="width"/> and the widest loaded key.</summary>
    public static ListColumns For(int width, int loadedKeyWidth)
    {
        var key = Math.Clamp(loadedKeyWidth, MinKeyWidth, MaxKeyWidth);
        var assignee = width >= AssigneeMinimumWidth ? AssigneeWidth : 0;
        var status = width >= StatusNameMinimumWidth ? FullStatus : GlyphWidth;
        var gaps = assignee > 0 ? 4 : 3;
        var summary = width - MarkWidth - key - MarkerWidth - status - assignee - gaps;

        return new ListColumns(key, Math.Max(0, summary), status, assignee);
    }

    /// <summary>
    /// The columns plus the optional ones the rows in view have data for (sprint, estimate, epic; in that order of
    /// priority). An optional column is added only while the summary keeps <see cref="MinSummaryWidth"/> cells.
    /// </summary>
    public static ListColumns For(int width, int loadedKeyWidth, bool wantSprint, bool wantPoints, bool wantEpic)
    {
        var columns = For(width, loadedKeyWidth);
        var summary = columns.Summary;
        var sprint = Fit(wantSprint, SprintWidth, ref summary);
        var points = Fit(wantPoints, PointsWidth, ref summary);
        var epic = Fit(wantEpic, EpicWidth, ref summary);

        return columns with { Summary = summary, Sprint = sprint, Points = points, Epic = epic };
    }

    private static int Fit(bool wanted, int columnWidth, ref int summary)
    {
        if (!wanted || summary - columnWidth - 1 < MinSummaryWidth)
        {
            return 0;
        }

        summary -= columnWidth + 1;

        return columnWidth;
    }
}
