using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Suggestions;

/// <summary>The popup of the tab closed: stop its lookup, whatever stage it is in.</summary>
public sealed record CancelSuggestionsEffect(TabId Tab) : Effect;
