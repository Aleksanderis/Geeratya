using System.Globalization;
using Geeratya.Core.Backlog;
using Geeratya.Core.Boards;
using Geeratya.Core.Clipboard;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Flash;
using Geeratya.Core.Forms;
using Geeratya.Core.Help;
using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Links;
using Geeratya.Core.Lists;
using Geeratya.Core.Panes;
using Geeratya.Core.Selection;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;

namespace Geeratya.Core.State;

public sealed partial class AppReducer
{
    private ReduceResult Apply(AppState state, string action, DateTimeOffset at)
    {
        if (SettingsScreenReducer.Handle(state, action) is { } onScreen)
        {
            return onScreen;
        }

        if (DetailUnderlayGate.Refuses(state, action))
        {
            return Result(state);
        }

        if (ClosedSprintGate.Refuses(state, action))
        {
            return Result(state with { Message = ClosedSprintGate.Refusal });
        }

        if (EmptySprintFocus.Refuses(state, action))
        {
            return Result(state with { Message = EmptySprintFocus.Refusal });
        }

        if (state.Editor is null && PromptEditing.OperationOf(action) is { } operation)
        {
            return Result(PromptEditing.Apply(state, operation));
        }

        return action switch
        {
            ActionIds.Quit => Quit(state),
            ActionIds.HelpToggle => Result(ToggleHelp(state)),
            ActionIds.HelpClose => Result(HelpReducer.Close(state) ?? ToNormal(state)),
            ActionIds.HelpFilter => Result(HelpReducer.OpenFilter(state)),
            ActionIds.HelpPageNext => Result(HelpReducer.Step(state, _keymap, 1)),
            ActionIds.HelpPagePrevious => Result(HelpReducer.Step(state, _keymap, -1)),
            ActionIds.HelpScrollDown => Result(ScrollHelp(state, 1)),
            ActionIds.HelpScrollUp => Result(ScrollHelp(state, -1)),
            ActionIds.HelpHalfPageDown => Result(ScrollHelp(state, HelpHalfPage)),
            ActionIds.HelpHalfPageUp => Result(ScrollHelp(state, -HelpHalfPage)),
            ActionIds.HelpTop => Result(ScrollHelp(state, int.MinValue / 2)),
            ActionIds.HelpBottom => Result(ScrollHelp(state, int.MaxValue / 2)),
            ActionIds.StatusHistory => InNormal(state, () => Result(StatusMessages.OpenHistory(state))),
            ActionIds.StatusDismiss => Result(StatusMessages.Dismiss(state)),
            ActionIds.ViewCycleCommandLevel => Result(CycleCommandLevel(state)),
            ActionIds.SettingsOpen => InNormal(state, () => SettingsScreenReducer.Toggle(state)),
            ActionIds.SettingsReset => Result(state with { Message = SettingsResetHint }),
            ActionIds.PaletteOpen => Result(ToNormal(state) with { Mode = InputMode.Command }),
            ActionIds.ContextMenuOpen => InNormal(state, () => OpenContextMenu(state)),
            ActionIds.ViewMenuOpen => InNormal(state, () => OpenViewMenu(state)),
            ActionIds.PaletteBackspace => Result(Backspace(state)),
            ActionIds.PaletteSubmit => SubmitPalette(state, at),
            ActionIds.Cancel when state.Editor is null && PaneActions.LeaveZoom(state) is { } unzoomed
                => Result(unzoomed with { Message = null }),
            ActionIds.Cancel => state.Editor is null ? Result(ToNormal(Escaped(state))) : CommentEditing.Cancel(state),
            ActionIds.PaneFocusLeft or ActionIds.PaneFocusDown or ActionIds.PaneFocusUp or ActionIds.PaneFocusRight
                or ActionIds.PaneZoom => InNormal(
                    state,
                    () => DetailActions.FocusPane(state, action) ?? Result(PaneActions.Apply(state, action) ?? state)),
            ActionIds.SelectionVisual => InNormal(
                state,
                () => DetailYank.ToggleVisual(state) ?? SelectionActions.ToggleVisual(state)),
            ActionIds.DetailYank => InNormal(
                state,
                () => DetailYank.Yank(state) ?? IssueYank.Copy(state, IssueYankForm.Key)),
            ActionIds.IssueYankKey => InNormal(state, () => IssueYank.Copy(state, IssueYankForm.Key)),
            ActionIds.IssueYankLink => InNormal(state, () => IssueYank.Copy(state, IssueYankForm.Link)),
            ActionIds.IssueYankSummary => InNormal(state, () => IssueYank.Copy(state, IssueYankForm.Summary)),
            ActionIds.SelectionMark => InNormal(state, () => SelectionActions.ToggleMark(state, true)),
            ActionIds.RankUp => InNormal(state, () => RankActions.Step(state, RankMove.Up, 1, at)),
            ActionIds.RankDown => InNormal(state, () => RankActions.Step(state, RankMove.Down, 1, at)),
            ActionIds.RankTop => InNormal(state, () => RankActions.Step(state, RankMove.Top, 1, at)),
            ActionIds.RankBottom => InNormal(state, () => RankActions.Step(state, RankMove.Bottom, 1, at)),
            ActionIds.RankTo => InNormal(state, () => RankActions.PickUpOrDrop(state, DropKeys(), at)),
            ActionIds.ConfirmNo => DeclineConfirm(state),
            ActionIds.ConfirmYes => Confirmed(state, at),
            ActionIds.FormNext => Accept(state, at),
            ActionIds.FormBackspace => Result(Erase(state)),
            ActionIds.ProfileRemove => ProfileReducer.RemoveHighlighted(state),
            ActionIds.ProfileAdd => InProfileList(state, ProfileReducer.Add),
            ActionIds.ProfileEdit => InProfileList(state, current => ProfileReducer.EditHighlighted(current)),
            ActionIds.ProfileTest => ProfileReducer.TestConnection(state),
            ActionIds.FormFieldNext when state.Form is not null => Result(ProfileReducer.Focus(state, 1)),
            ActionIds.FormFieldPrevious when state.Form is not null => Result(ProfileReducer.Focus(state, -1)),
            ActionIds.PickerNext => Result(MovePicker(state, 1)),
            ActionIds.PickerPrevious => Result(MovePicker(state, -1)),
            ActionIds.PickerOpenNewTab => state.Picker is null ? Result(state) : SourceReducer.AcceptInNewTab(state, at),
            ActionIds.SprintGoal => InNormal(state, () => Result(SprintGoalNotice.Open(state))),
            ActionIds.ViewSavedOpen => InNormal(state, () => SourceReducer.OpenSavedViews(state)),
            ActionIds.ViewSavedSave => InNormal(state, () => OpenSaveCommand(state)),
            ActionIds.SprintSwitch => InNormal(state, () => SourceReducer.OpenSprintPicker(state)),
            ActionIds.SprintMoveIssues => InNormal(state, () => SprintMoveActions.OpenPicker(state)),
            ActionIds.SprintMoveToBacklog => InNormal(state, () => SprintMoveActions.ToBacklog(state, at)),
            ActionIds.SprintCreate => InNormal(state, () => SprintForms.Create(state)),
            ActionIds.SprintEdit => InNormal(state, () => SprintForms.Edit(state)),
            ActionIds.SprintStart => InNormal(state, () => SprintLifecycle.Start(state, at)),
            ActionIds.SprintComplete => InNormal(state, () => SprintLifecycle.Complete(state, at)),
            ActionIds.IssueJump => InNormal(state, () => JumpReducer.Open(state)),
            ActionIds.RowJump => InNormal(state, () => FlashJumpReducer.Open(state)),
            ActionIds.FormSubmit when state.Form is not null => ProfileReducer.Save(state),
            ActionIds.FormSubmit => CommentEditing.Save(state, at),
            ActionIds.EditorLeft => Result(CommentEditing.Move(state, EditorMove.Left)),
            ActionIds.EditorRight => Result(CommentEditing.Move(state, EditorMove.Right)),
            ActionIds.EditorRowStart => Result(CommentEditing.Move(state, EditorMove.RowStart)),
            ActionIds.EditorRowEnd => Result(CommentEditing.Move(state, EditorMove.RowEnd)),
            ActionIds.EditorPageUp => Result(CommentEditing.Move(state, EditorMove.PageUp)),
            ActionIds.EditorPageDown => Result(CommentEditing.Move(state, EditorMove.PageDown)),
            ActionIds.EditorWordLeft => Result(CommentEditing.Move(state, EditorMove.WordLeft)),
            ActionIds.EditorWordRight => Result(CommentEditing.Move(state, EditorMove.WordRight)),
            ActionIds.EditorDeleteWordBefore => Result(CommentEditing.DeleteWordBefore(state)),
            ActionIds.EditorDeleteWordAfter => Result(CommentEditing.DeleteWordAfter(state)),
            ActionIds.EditorDeleteToStart => Result(CommentEditing.DeleteToStart(state)),
            ActionIds.EditorDelete => Result(CommentEditing.DeleteForward(state)),
            ActionIds.EditorExternal when state.Form is null => CommentEditing.External(state),
            ActionIds.CommentAdd => InNormal(state, () => CommentEditing.Add(state)),
            ActionIds.CommentEdit => InNormal(state, () => CommentEditing.Edit(state)),
            ActionIds.CommentDelete => InNormal(state, () => CommentEditing.Delete(state, at)),
            ActionIds.NavigateDown => Navigate(state, ListMove.Down, 1),
            ActionIds.NavigateUp => Navigate(state, ListMove.Up, -1),
            ActionIds.NavigateTop => Navigate(state, ListMove.Top, 0),
            ActionIds.NavigateBottom => Navigate(state, ListMove.Bottom, 0),
            ActionIds.NavigatePageDown => Navigate(state, ListMove.PageDown, 0),
            ActionIds.NavigatePageUp => Navigate(state, ListMove.PageUp, 0),
            ActionIds.NavigateLeft => NavigateColumn(state, BoardColumnMove.Left),
            ActionIds.NavigateRight => NavigateColumn(state, BoardColumnMove.Right),
            ActionIds.NavigateFirstColumn => NavigateColumn(state, BoardColumnMove.First),
            ActionIds.NavigateLastColumn => NavigateColumn(state, BoardColumnMove.Last),
            ActionIds.ViewLayoutToggle => ToggleLayout(state),
            ActionIds.ViewQuickFilter => InNormal(state, () => BoardViewOptions.CycleFilter(state)),
            ActionIds.ViewSwimlane => InNormal(state, () => BoardViewOptions.CycleSwimlane(state)),
            ActionIds.ViewDoneCutoff => InNormal(state, () => DoneCutoffPicker.Open(state)),
            ActionIds.ItemOpen => state.Mode == InputMode.Normal ? OpenItem(state) : Result(state),
            ActionIds.SectionNext => InNormal(
                state,
                () => BacklogActions.Jump(state, 1) ?? BoardViewOptions.StepLane(state, 1) ?? Result(state)),
            ActionIds.SectionPrevious => InNormal(
                state,
                () => BacklogActions.Jump(state, -1) ?? BoardViewOptions.StepLane(state, -1) ?? Result(state)),
            ActionIds.SectionToggle => InNormal(
                state,
                () => DetailActions.ToggleText(state)
                    ?? BacklogActions.Toggle(state)
                    ?? SubtaskActions.Toggle(state)
                    ?? Result(state)),
            ActionIds.DetailEdit => InNormal(state, () => DetailActions.EditText(state)),
            ActionIds.IssueOpenParent => InNormal(state, () => DetailActions.OpenParent(state)),
            ActionIds.IssueTransition => InNormal(state, () => TransitionForms.OpenPicker(state)),
            ActionIds.IssueTransitionNext => InNormal(state, () => TransitionForms.MoveCard(state, 1)),
            ActionIds.IssueTransitionPrevious => InNormal(state, () => TransitionForms.MoveCard(state, -1)),
            ActionIds.TabNext => ViewReducer.StepTab(state, 1),
            ActionIds.TabPrevious => ViewReducer.StepTab(state, -1),
            ActionIds.TabLastActive => ViewReducer.LastActiveTab(state),
            ActionIds.TabNew => SourceReducer.OpenPicker(state),
            ActionIds.TabRename => InNormal(state, () => OpenRenameCommand(state)),
            ActionIds.TabClose => ViewReducer.CloseActiveTab(state),
            ActionIds.TabList => TabListReducer.Open(state),
            ActionIds.IssueDelete => InNormal(state, () => DeleteReducer.Start(state)),
            ActionIds.IssueCreate => InNormal(state, () => FormReducer.Reduce(state, new OpenCreateFormRequested(false))),
            ActionIds.IssueCreateSubtask => InNormal(
                state,
                () => FormReducer.Reduce(state, new OpenCreateFormRequested(true))),
            ActionIds.IssueUnlink => InNormal(state, () => LinkReducer.Unlink(state, at)),
            ActionIds.IssueLink => InNormal(state, () => FormReducer.Reduce(state, new OpenLinkFormRequested())),
            ActionIds.IssueEdit => InNormal(state, () => FormOpener.Edit(state)),
            ActionIds.IssueAssign => InNormal(state, () => FormOpener.Assign(state)),
            ActionIds.IssueAssignMe => InNormal(state, () => FormOpener.AssignMe(state, at)),
            ActionIds.IssueOpenInBrowser => DetailActions.OpenInBrowser(state) ?? BrowserReducer.OpenIssue(state, null),
            ActionIds.ViewRefresh => ViewReducer.RefreshActive(state),
            _ when TabNumber(action) is { } number => ViewReducer.GoToTab(state, number),
            _ => Result(state),
        };
    }

    /// <summary>
    /// Enter: a remote link or attachment row opens in the browser; else a row, related issue or comment page.
    /// </summary>
    private static ReduceResult OpenItem(AppState state)
    {
        return BacklogActions.Enter(state) ?? DetailActions.EnterBrowserRow(state) ?? ViewReducer.OpenSelected(state);
    }

    private static ReduceResult InNormal(AppState state, Func<ReduceResult> action)
    {
        return state.Mode == InputMode.Normal ? action() : Result(state);
    }

    private static ReduceResult InProfileList(AppState state, Func<AppState, ReduceResult> action)
    {
        return state is { ProfileList: not null, Form: null } ? action(state) : Result(state);
    }

    /// <summary>Enter in Insert mode: a new line in the editor, else accepts the form field or picker row.</summary>
    private ReduceResult Accept(AppState state, DateTimeOffset at)
    {
        if (state.Editor is not null)
        {
            return Result(CommentEditing.Type(state, "\n"));
        }

        if (state.Jump is not null)
        {
            return JumpReducer.Accept(state);
        }

        if (state.FlashJump is not null)
        {
            return FlashJumpReducer.Accept(state);
        }

        if (state.ContextMenu is { } menu)
        {
            return RunMenuItem(state, menu.Items[Math.Clamp(menu.Selected, 0, menu.Items.Count - 1)], at);
        }

        if (state is { ProfileList: not null, Form: null })
        {
            return ProfileReducer.SwitchHighlighted(state);
        }

        return state.Picker is null ? ProfileReducer.Submit(state) : SourceReducer.Accept(state, at);
    }

    /// <summary>Up and down in Insert mode: a row up or down in the editor, else the picker selection.</summary>
    private AppState MovePicker(AppState state, int direction)
    {
        if (state.Mode == InputMode.Command)
        {
            return MovePalette(state, direction);
        }

        if (state.Jump is not null)
        {
            return JumpReducer.Move(state, direction);
        }

        if (state.ContextMenu is { } menu)
        {
            return state with { ContextMenu = ContextMenus.Move(menu, direction) };
        }

        if (state is { ProfileList: not null, Form: null })
        {
            return ProfileReducer.MoveList(state, direction);
        }

        if (state.Editor is null)
        {
            return SourceReducer.Move(state, direction);
        }

        return CommentEditing.Move(state, direction > 0 ? EditorMove.Down : EditorMove.Up);
    }

    /// <summary>n or Esc in Confirm mode; the discard question goes back to the editor with its text.</summary>
    private static ReduceResult DeclineConfirm(AppState state)
    {
        if (state.Editor is not null)
        {
            return Result(CommentEditing.KeepEditing(state));
        }

        if (state.Confirm?.Title is not null)
        {
            return Result(ToNormal(state));
        }

        return Result(ToNormal(state) with { Message = "Cancelled." });
    }

    private static AppState Erase(AppState state)
    {
        if (state.Editor is not null)
        {
            return CommentEditing.Backspace(state);
        }

        if (state.Jump is not null)
        {
            return JumpReducer.Backspace(state);
        }

        if (state.FlashJump is not null)
        {
            return PromptEditing.Backspace(state);
        }

        return state.Picker is null ? ProfileReducer.Backspace(state) : SourceReducer.Backspace(state);
    }

    private string DropKeys()
    {
        var keys = _keymap.FindKeys(InputMode.Normal, ActionIds.RankTo);

        return keys is null ? "the same key" : KeyNotation.Display(keys);
    }

    private static int? TabNumber(string action)
    {
        const string prefix = "tab.go-to.";

        if (!action.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parsed = int.TryParse(action.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number);

        return parsed ? number : null;
    }

    private ReduceResult Confirmed(AppState state, DateTimeOffset at)
    {
        var request = state.Confirm;
        var normal = ToNormal(state);

        if (request is null)
        {
            return Result(normal);
        }

        if (request.Title is not null)
        {
            return Result(normal);
        }

        if (request.ActionOnConfirm == ProfileReducer.RemoveAction)
        {
            return ProfileReducer.Remove(normal, request.Target);
        }

        if (request.ActionOnConfirm == ProfileReducer.SwitchAction)
        {
            return ProfileReducer.ConfirmedSwitch(normal, request.Target);
        }

        if (request.OnConfirm is { } command)
        {
            return Reduce(normal, command);
        }

        // The quit that was asked for while writes ran: the answer was yes, so no second question.
        return request.ActionOnConfirm == ActionIds.Quit
            ? new ReduceResult(normal, _quitEffects)
            : Apply(normal, request.ActionOnConfirm, at);
    }
}
