namespace Geeratya.Core.Detail;

/// <summary>Limits of the issue detail loads.</summary>
public static class DetailPaging
{
    /// <summary>Comments requested per page.</summary>
    public const int CommentPageSize = 50;

    /// <summary>Comments kept per opened issue; further pages are not offered.</summary>
    public const int MaxComments = 1000;

    /// <summary>The lowest hierarchy level of an issue type whose children are loaded (epics are level 1).</summary>
    public const int ParentLevel = 1;

    /// <summary>Opened issues kept on one tab's stack.</summary>
    public const int MaxStack = 50;
}
