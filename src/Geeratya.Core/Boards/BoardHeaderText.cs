namespace Geeratya.Core.Boards;

/// <summary>
/// The texts of a column header, shared by the renderer and the mouse hit test so a click lands on what is drawn.
/// </summary>
public static class BoardHeaderText
{
    /// <summary>Cells of the corner and rule that start a one-column header (<c>┌─</c>).</summary>
    public const int CornerWidth = 2;

    private const string HiddenLeftMark = "‹";
    private const string HiddenRightMark = "›";
    private const string MoreMark = "+";

    /// <summary>The column name, its card count and a <c>+</c> while more rows can still load.</summary>
    public static string Title(BoardColumnGroup column, bool moreLoads)
    {
        return $"{column.Name} {column.Rows.Count}{(moreLoads ? MoreMark : string.Empty)}";
    }

    /// <summary>The previous column as named left of the title of a one-column header.</summary>
    public static string LeftNeighbour(BoardColumnGroup column, bool moreLoads)
    {
        return $"{HiddenLeftMark} {Title(column, moreLoads)}  ";
    }

    /// <summary>The next column as named right of the title of a one-column header.</summary>
    public static string RightNeighbour(BoardColumnGroup column, bool moreLoads)
    {
        return $"  {Title(column, moreLoads)} {HiddenRightMark}";
    }
}
