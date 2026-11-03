using Geeratya.Core.Comments;
using Geeratya.Core.Forms;
using Geeratya.Core.Help;
using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Sources;
using Geeratya.Core.Views;

namespace Geeratya.Core.State;

public sealed partial class AppReducer
{
    /// <summary>
    /// A click on an overlay row: the row is chosen, then the key's handler runs (Enter takes it, Tab focuses a field,
    /// Ctrl-x removes a profile), so a click and a key end in the same state.
    /// </summary>
    private ReduceResult ReduceOverlayRow(AppState state, OverlayRowClicked clicked)
    {
        var cleared = state with { PendingKeys = string.Empty, Count = 0, Message = null };

        if (clicked.Target is MouseTarget.PickerRow or MouseTarget.JumpRow
            && clicked.Action is ActionIds.PickerNext or ActionIds.PickerPrevious)
        {
            // The wheel over the rows moves the highlight and opens nothing.
            var rows = clicked.Action == ActionIds.PickerNext ? DefaultMouseMap.WheelRows : -DefaultMouseMap.WheelRows;

            return Result(MovePicker(cleared, rows));
        }

        return clicked.Target switch
        {
            MouseTarget.PickerRow when cleared.Picker is { } picker
                => SourceReducer.Accept(cleared with { Picker = picker with { Selected = clicked.Index } }, clicked.At),
            MouseTarget.JumpRow when cleared.Jump is { } jump
                => JumpReducer.Accept(cleared with { Jump = jump with { Selected = clicked.Index } }),
            MouseTarget.SettingsCategory => SettingsScreenReducer.GoToCategory(cleared, clicked.Index),
            MouseTarget.SettingsRow => SettingsScreenReducer.Clicked(cleared, clicked.Index, clicked.Action),
            MouseTarget.SettingsChoice => SettingsScreenReducer.ChoiceClicked(cleared, clicked.Index),
            MouseTarget.ProfileRow when clicked.Action == ActionIds.ProfileRemove
                => ProfileReducer.RemoveHighlighted(cleared, clicked.Index),
            MouseTarget.ProfileRow => ProfileReducer.SwitchHighlighted(cleared, clicked.Index),
            MouseTarget.ProfileAddButton when cleared.ProfileList is not null => ProfileReducer.Add(cleared),
            MouseTarget.ProfileEditButton when cleared.ProfileList is not null
                => ProfileReducer.EditHighlighted(cleared),
            MouseTarget.ProfileTestButton when cleared.Form is not null => ProfileReducer.TestConnection(cleared),
            MouseTarget.ProfileSaveButton when cleared.Form is not null => ProfileReducer.Save(cleared),
            MouseTarget.ProfileCancelButton when cleared.Form is not null => Result(ToNormal(cleared)),
            MouseTarget.FormField when cleared.Form is not null
                => Result(ProfileReducer.FocusField(cleared, clicked.Index)),
            MouseTarget.FormField or MouseTarget.FormChoice => FormReducer.Clicked(cleared, clicked),
            MouseTarget.ContextMenu when cleared.ContextMenu is { } menu && clicked.Index < menu.Items.Count
                => RunMenuItem(cleared, menu.Items[clicked.Index], clicked.At),
            MouseTarget.EditorChoice => EditorSuggestions.Clicked(cleared, clicked.Index),
            MouseTarget.PaletteRow when state.Mode == InputMode.Command => ClickPaletteRow(cleared, clicked),
            MouseTarget.HelpPage when state.Mode == InputMode.Help => Result(HelpReducer.Show(cleared, clicked.Index)),
            MouseTarget.ConfirmYes when state.Mode == InputMode.Confirm => Confirmed(state, clicked.At),
            MouseTarget.ConfirmNo when state.Mode == InputMode.Confirm => DeclineConfirm(state),
            _ => Result(state),
        };
    }

    /// <summary>
    /// A click on the body: like an action it drops a half-typed chord, a count and the notice, then places the
    /// cursor and, when something was hit, runs the action that came with the gesture on it.
    /// </summary>
    private ReduceResult Pointed(AppState state, PointerPlaced placed)
    {
        var cleared = Unpended(state);

        if (ViewReducer.PlaceCursor(cleared, placed) is not { } placement)
        {
            return Result(cleared);
        }

        if (placed.Then is not { } action)
        {
            return IsSubtaskRowClick(cleared, placed) ? WithPlacement(placement, _subtaskClick) : placement;
        }

        // A mark needs a card under the pointer: a column header only moves the cursor there.
        if (action.Action == ActionIds.SelectionMark && !CardUnderPointer(cleared, placed))
        {
            return placement;
        }

        var acted = ReduceRequested(placement.State, action);

        return new ReduceResult(acted.State, [.. placement.Effects, .. acted.Effects]);
    }

    private ReduceResult WithPlacement(ReduceResult placement, ActionRequested action)
    {
        var acted = ReduceRequested(placement.State, action);

        return new ReduceResult(acted.State, [.. placement.Effects, .. acted.Effects]);
    }

    // A plain click on the sub-task row of a card toggles its list (mouse table: SubtaskRow), the same handler as z a.
    private static bool IsSubtaskRowClick(AppState before, PointerPlaced placed)
    {
        var tab = before.Session.Active is { } id ? ViewReducer.Find(before.Session.Tabs, id) : null;

        return tab is { Detail.Count: 0, Definition.Layout: ViewLayout.Columns }
            && ColumnsNavigation.IsSubtaskRowAt(tab, placed.Width, placed.Column, placed.Row);
    }

    private static bool CardUnderPointer(AppState before, PointerPlaced placed)
    {
        var tab = before.Session.Active is { } id ? ViewReducer.Find(before.Session.Tabs, id) : null;

        return tab is not { Detail.Count: 0, Definition.Layout: ViewLayout.Columns }
            || ColumnsNavigation.IsCardAt(tab, placed.Width, placed.Column, placed.Row);
    }

    private static AppState Unpended(AppState state)
    {
        if (state.PendingKeys.Length == 0 && state.Count == 0 && state.Message is null)
        {
            return state;
        }

        return state with { PendingKeys = string.Empty, Count = 0, Message = null };
    }
}
