using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;

namespace Geeratya.Core.Settings;

/// <summary>
/// The user's switches (<c>settings.json</c> in the configuration directory). Both hint helps are on (hints
/// <c>full</c>) by default.
/// </summary>
/// <param name="StatusHints">How much of the key hints the right of the status bar shows.</param>
/// <param name="WhichKey">Show the popup of the next keys while a multi-key binding is half typed.</param>
/// <param name="CommandLevel">Which actions help, the palette, which-key and hints list: everyday or advanced.</param>
/// <param name="Theme">The name of the colour theme: a built-in one or a file in the <c>themes</c> folder.</param>
/// <param name="RestoreTabs">Reopen the tabs of the last run on start, and remember them while running.</param>
/// <param name="JumpTarget">Whether the jump prompt opens an issue in the current tab or in a new one.</param>
/// <param name="TransparentBackground">Keep the terminal's own background under the page instead of the theme's.</param>
/// <param name="LabelCaps">Round the ends of filled chips (labels) with Powerline half circles; needs a Powerline or Nerd Font.</param>
/// <param name="DoneCutoffDays">Days after which a Kanban board hides Done issues; 0 shows them all.</param>
public sealed record AppSettings(
    HintLevel StatusHints = HintLevel.Full,
    bool WhichKey = true,
    CommandLevel CommandLevel = CommandLevel.Everyday,
    string Theme = AppSettings.DefaultTheme,
    JumpTarget JumpTarget = JumpTarget.Current,
    bool RestoreTabs = true,
    bool TransparentBackground = false,
    int DoneCutoffDays = DoneCutoff.DefaultDays,
    bool LabelCaps = true)
{
    public const string DefaultTheme = "auto";

    public static AppSettings Default { get; } = new();
}
