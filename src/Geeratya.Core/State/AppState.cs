using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>Everything the shell shows; a new value comes out of every command and nothing mutates it.</summary>
/// <param name="Mode">The current input mode.</param>
/// <param name="PendingKeys">Keys typed so far of a multi-key binding such as <c>g g</c>, empty otherwise.</param>
/// <param name="PaletteText">The text typed after <c>:</c> in Command mode.</param>
/// <param name="HelpScroll">The first visible line of the help overlay.</param>
/// <param name="Message">A one-line notice for the status line, or null.</param>
/// <param name="Confirm">The open confirmation in Confirm mode, otherwise null.</param>
/// <param name="Profiles">The saved profiles and the active one.</param>
/// <param name="Form">The add-profile form in Insert mode, otherwise null.</param>
/// <param name="ProfileBusy">A profile change is being saved; further profile changes are refused until it ends.</param>
public sealed record AppState(
    InputMode Mode,
    string PendingKeys,
    string PaletteText,
    int HelpScroll,
    string? Message,
    ConfirmRequest? Confirm,
    ProfileCatalog Profiles,
    ProfileForm? Form,
    bool ProfileBusy)
{
    public static AppState Initial { get; } =
        new(InputMode.Normal, string.Empty, string.Empty, 0, null, null, ProfileCatalog.Empty, null, false);
}
