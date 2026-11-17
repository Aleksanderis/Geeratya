using Geeratya.Core.Lists;

namespace Geeratya.Core.Menus;

/// <summary>An open right-click menu (Insert mode overlay): the actions of the row or line it was opened on.</summary>
/// <param name="Items">The actions offered, in order; never empty.</param>
/// <param name="Pick">The highlighted item (<see cref="PickList"/>).</param>
public sealed record ContextMenuState(IReadOnlyList<ContextMenuItem> Items, PickList Pick)
{
    /// <summary>A menu with the highlight on item <paramref name="selected"/>.</summary>
    public ContextMenuState(IReadOnlyList<ContextMenuItem> items, int selected)
        : this(items, new PickList(selected))
    {
    }

    /// <summary>The highlighted item.</summary>
    public int Selected
    {
        get => Pick.Selected;
        init => Pick = Pick with { Selected = value };
    }
}

/// <summary>One action of the menu: the id its key would run and the title shown.</summary>
public sealed record ContextMenuItem(string Action, string Title);
