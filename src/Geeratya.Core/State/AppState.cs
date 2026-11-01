using Geeratya.Core.Keymap;

namespace Geeratya.Core.State;

/// <summary>Everything the shell shows; a new value comes out of every command and nothing mutates it.</summary>
/// <param name="Mode">The current input mode.</param>
/// <param name="PendingKeys">Keys typed so far of a multi-key binding such as <c>g g</c>, empty otherwise.</param>
/// <param name="PaletteText">The text typed after <c>:</c> in Command mode.</param>
/// <param name="HelpScroll">The first visible line of the help overlay.</param>
/// <param name="Message">A one-line notice for the status line, or null.</param>
/// <param name="Confirm">The open confirmation in Confirm mode, otherwise null.</param>
public sealed record AppState(
    InputMode Mode,
    string PendingKeys,
    string PaletteText,
    int HelpScroll,
    string? Message,
    ConfirmRequest? Confirm)
{
    public static AppState Initial { get; } = new(InputMode.Normal, string.Empty, string.Empty, 0, null, null);
}
