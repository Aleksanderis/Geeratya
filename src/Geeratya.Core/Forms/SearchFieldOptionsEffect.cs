using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Forms;

/// <summary>
/// Search the options of a searchable field of a form for the typed text (the people who can be assigned the issue).
/// Debounced and cancelled by the next search of the tab; answered with <see cref="FieldOptionsLoaded"/> or
/// <see cref="FieldOptionsFailed"/>.
/// </summary>
public sealed record SearchFieldOptionsEffect(TabId Tab, long RequestId, string Key, string Query) : Effect;
