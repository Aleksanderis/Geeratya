namespace Geeratya.Core.Views;

/// <summary>Paging and memory bounds of the views (<c>docs/architecture.md</c>, "Views and tabs").</summary>
public static class ViewPaging
{
    /// <summary>Issues requested per page.</summary>
    public const int PageSize = 100;

    /// <summary>The most rows one tab holds; more are dropped and the tab is marked truncated.</summary>
    public const int MaxRowsPerTab = 10_000;

    /// <summary>The most rows held across all tabs, pending rows included; hidden tabs go cold above it.</summary>
    public const int MaxRowsTotal = 50_000;
}
