namespace Geeratya.Core.Views;

/// <summary>Why a tab is loading; it decides whether the reply replaces or extends the rows.</summary>
public enum LoadReason
{
    /// <summary>The view was just opened (or a cold tab was activated).</summary>
    Open,

    /// <summary>The next page of a list is appended to the rows.</summary>
    NextPage,

    /// <summary>The rows are reloaded and replaced as a whole.</summary>
    Refresh,

    /// <summary>A failed load is tried again.</summary>
    Retry,
}
