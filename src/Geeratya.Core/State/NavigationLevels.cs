using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Panes;
using Geeratya.Core.Selection;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;

namespace Geeratya.Core.State;

/// <summary>
/// The one "close one level" rule behind <c>Esc</c> and behind <c>q</c> wherever <c>q</c> is not text
/// (<c>docs/internal/ui-consistency.md</c>, decision 2). One call closes the innermost open thing: a popup, a typed
/// filter, field editing, a form, an overlay, Visual mode or marks, a rank pickup, zoom, the issue stack. Only at the
/// view root, with nothing left to close, does <c>q</c> quit. Pure; the form's own steps (popup, filter, field
/// editing, discard prompt) live in <c>FormReducer</c> and are reached through the same keys.
/// </summary>
public static class NavigationLevels
{
    /// <summary>The key that closes a level in every state where it is not text.</summary>
    public const string CloseKey = "q";

    private const string DiscardQuestion = "Discard the changes to the profile?";

    /// <summary>True when there is nothing left to close: <c>q</c> then quits.</summary>
    public static bool AtRoot(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return state.Mode == InputMode.Normal
            && state.SettingsScreen is null
            && state.Editor is null
            && state.Form is null
            && state.Picker is null
            && state.Jump is null
            && state.FlashJump is null
            && state.ProfileList is null
            && state.ContextMenu is null
            && state.Confirm is null
            && !state.MessagesOpen
            && tab?.RankPickup is null
            && tab?.Form is null
            && !SelectionActions.CanBack(state)
            && !DetailYank.CanBack(state)
            && !PaneActions.IsZoomed(state)
            && !ViewReducer.CanGoBack(state);
    }

    /// <summary>
    /// True when <c>q</c> closes a level instead of typing: Insert mode with only a menu or the profile manager open
    /// (no text field takes the key there).
    /// </summary>
    public static bool QClosesInsert(AppState state)
    {
        return state.Mode == InputMode.Insert
            && state.Form is null
            && state.Picker is null
            && state.Jump is null
            && state.FlashJump is null
            && state.Editor is null
            && (state.ContextMenu is not null || state.ProfileList is not null);
    }

    /// <summary>The state with the typed filter of the open palette, picker, Jump or Flash cleared; null when none.</summary>
    public static AppState? ClearTyped(AppState state)
    {
        if (state.Mode == InputMode.Command && state.PaletteText.Length > 0)
        {
            return state with { PaletteText = string.Empty, PaletteBack = 0, PalettePick = PickList.First };
        }

        if (state.Picker is { Text.Length: > 0 } picker)
        {
            return state with { Picker = picker with { Text = string.Empty, Back = 0, Pick = PickList.First, Notice = null } };
        }

        if (state.Jump is { Text.Length: > 0 } jump)
        {
            return state with { Jump = jump with { Text = string.Empty, Back = 0, Pick = PickList.First, Notice = null } };
        }

        if (state.FlashJump is { Text.Length: > 0 } flash)
        {
            return state with { FlashJump = flash with { Text = string.Empty, Back = 0 } };
        }

        return null;
    }

    /// <summary>
    /// A profile form with changes asks before it closes (a confirm that returns to the form on <c>n</c>); null when
    /// no profile form is open or it has no changes.
    /// </summary>
    public static AppState? AskDiscardProfile(AppState state)
    {
        if (state.Form is not { } form || state.Mode != InputMode.Insert || !ProfileReducer.IsDirty(state, form))
        {
            return null;
        }

        var request = new ConfirmRequest(DiscardQuestion, ProfileReducer.DiscardAction, Label: "Discard");

        return state with { Mode = InputMode.Confirm, Confirm = request, PendingKeys = string.Empty };
    }

    /// <summary>The profile manager opened from Settings closes back to Settings; null for any other state.</summary>
    public static AppState? LeaveManager(AppState state)
    {
        if (state.ProfileList is not { ReturnTo: { } screen } || state.Form is not null || state.Confirm is not null)
        {
            return null;
        }

        return state with
        {
            Mode = InputMode.Normal,
            ProfileList = null,
            SettingsScreen = screen,
            PendingKeys = string.Empty,
        };
    }

    /// <summary>Visual mode, marks or a picked-up rank issue end first; null when the body holds none.</summary>
    public static AppState? LeaveBody(AppState state)
    {
        if ((DetailYank.Back(state) ?? SelectionActions.Back(state)) is { } selection)
        {
            return selection;
        }

        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return tab?.RankPickup is null ? null : RankActions.DropPickup(state);
    }
}
