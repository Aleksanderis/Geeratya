namespace Geeratya.Core.Lists;

/// <summary>Fixed measures of the list layout that the host and the renderer must agree on.</summary>
public static class ListLayout
{
    /// <summary>Rows above the issue rows: the top edge of the frame with the view title, and the column titles.</summary>
    public const int HeaderRows = 2;

    /// <summary>Rows under the issue rows: the bottom edge of the frame (<c>docs/ux-look.md</c>, section 2).</summary>
    public const int FooterRows = 1;
}
