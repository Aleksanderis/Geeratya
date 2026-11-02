using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Suggestions;

/// <summary>
/// Look up candidates for a popup. The host waits <paramref name="Debounce"/> (a newer effect of the tab cancels this
/// one during the wait or the call), then asks Jira. Answered with <see cref="SuggestionsLoaded"/>,
/// <see cref="SuggestionsFailed"/>, and <see cref="SuggestionsSlow"/> when the call takes long.
/// </summary>
public sealed record SearchSuggestionsEffect(
    TabId Tab,
    long RequestId,
    SuggestionSource Source,
    string Query,
    TimeSpan Debounce) : Effect;
