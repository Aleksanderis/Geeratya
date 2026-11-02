namespace Geeratya.Core.Suggestions;

/// <summary>Where a popup is in its lookup. The list shown is always what is loaded so far.</summary>
public enum SuggestionStatus
{
    /// <summary>The query changed; the lookup waits for the debounce or runs quickly.</summary>
    Waiting,

    /// <summary>The lookup takes longer than a moment: the popup says it is searching.</summary>
    Loading,

    /// <summary>The reply for the current query arrived.</summary>
    Loaded,

    /// <summary>The lookup failed; the popup shows what is loaded and says so.</summary>
    Failed,
}
