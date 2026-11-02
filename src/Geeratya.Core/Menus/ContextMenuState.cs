namespace Geeratya.Core.Menus;

/// <summary>An open right-click menu (Insert mode overlay): the actions of the row or line it was opened on.</summary>
/// <param name="Items">The actions offered, in order; never empty.</param>
/// <param name="Selected">The highlighted item.</param>
public sealed record ContextMenuState(IReadOnlyList<ContextMenuItem> Items, int Selected);

/// <summary>One action of the menu: the id its key would run and the title shown.</summary>
public sealed record ContextMenuItem(string Action, string Title);
