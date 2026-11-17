using Geeratya.Core.Lists;
using Geeratya.Core.Settings;

namespace Geeratya.Core.Profiles;

/// <summary>The open profile list (<c>:profile list</c>, Insert mode overlay).</summary>
/// <param name="Pick">The highlighted profile (<see cref="PickList"/>); clamped to the catalog when read.</param>
public sealed record ProfileListState(PickList Pick)
{
    /// <summary>A list with the highlight on profile <paramref name="selected"/>.</summary>
    public ProfileListState(int selected)
        : this(new PickList(selected))
    {
    }

    /// <summary>The highlighted profile; clamped to the catalog when read.</summary>
    public int Selected
    {
        get => Pick.Selected;
        init => Pick = Pick with { Selected = value };
    }

    /// <summary>The settings screen the manager was opened from; closing the manager returns to it. Null otherwise.</summary>
    public SettingsScreenState? ReturnTo { get; init; }
}
