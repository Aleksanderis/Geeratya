using Geeratya.Core.Settings;

namespace Geeratya.Core.Profiles;

/// <summary>The open profile list (<c>:profile list</c>, Insert mode overlay).</summary>
/// <param name="Selected">The highlighted profile; clamped to the catalog when read.</param>
public sealed record ProfileListState(int Selected)
{
    /// <summary>The settings screen the manager was opened from; closing the manager returns to it. Null otherwise.</summary>
    public SettingsScreenState? ReturnTo { get; init; }
}
