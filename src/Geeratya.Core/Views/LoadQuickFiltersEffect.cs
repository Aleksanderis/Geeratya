using Geeratya.Core.State;

namespace Geeratya.Core.Views;

/// <summary>
/// Read the quick filters of the board of a tab (#192). Data only; the host answers with
/// <see cref="QuickFiltersLoaded"/>. Asked for the first time a filter key or <c>:filter</c> is used on the tab.
/// </summary>
/// <param name="Tab">The tab the filters are for.</param>
/// <param name="Definition">The view, which names the board.</param>
/// <param name="Apply">A key started the lookup, so the key's action goes on once the filters are here.</param>
public sealed record LoadQuickFiltersEffect(TabId Tab, ViewDefinition Definition, bool Apply) : Effect;
