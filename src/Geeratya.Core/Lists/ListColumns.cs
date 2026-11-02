namespace Geeratya.Core.Lists;

/// <summary>
/// Column widths of the list layout for a terminal width (<c>docs/ux-screens.md</c>, "List layout"). A width of zero
/// means the column is dropped. Layout is data: the renderer only places text by these numbers.
/// </summary>
/// <param name="Key">Width of the key column.</param>
/// <param name="Summary">Width of the summary column (the remaining space).</param>
/// <param name="Status">Width of the status column: <see cref="FullStatus"/> or 1 (category glyph only).</param>
/// <param name="Assignee">Width of the assignee column, 0 when dropped.</param>
public readonly record struct ListColumns(int Key, int Summary, int Status, int Assignee)
{
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
}
