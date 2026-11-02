using Geeratya.Core.Forms;
using Geeratya.Core.Views;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Suggestions;

/// <summary>The lookup answered; applied only while the popup waits for that request.</summary>
public sealed record SuggestionsLoaded(TabId Tab, long RequestId, IReadOnlyList<Suggestion> Items) : FormCommand;

/// <summary>The lookup has run for a moment without an answer: the popup shows that it is searching.</summary>
public sealed record SuggestionsSlow(TabId Tab, long RequestId) : FormCommand;

/// <summary>The lookup failed; the popup keeps what is loaded and shows the message.</summary>
public sealed record SuggestionsFailed(TabId Tab, long RequestId, string Message) : FormCommand;
