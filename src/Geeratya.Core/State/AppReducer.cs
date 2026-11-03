using System.Globalization;
using Geeratya.Core.Backlog;
using Geeratya.Core.Boards;
using Geeratya.Core.Cache;
using Geeratya.Core.CommandPalette;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Help;
using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Links;
using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.Selection;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.Startup;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;

namespace Geeratya.Core.State;

/// <summary>
/// <c>(State, Command) -> (State, Effects)</c>. Holds only the keymap; no clock, no I/O, no terminal.
/// Esc and q follow <c>docs/ux.md</c>: Esc backs out one level, q quits only from Normal mode.
/// </summary>
public sealed class AppReducer
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];
    private static readonly IReadOnlyList<Effect> _quitEffects = [new QuitEffect()];

    private static readonly string[] _sourceVerbs =
    [
        PaletteCommands.Jql,
        PaletteCommands.Filter,
        PaletteCommands.Board,
        PaletteCommands.Backlog,
        PaletteCommands.View,
    ];

    private const string EscapeKey = "Esc";
    private const int HelpHalfPage = 8;
    private const string ConfirmTooSmallText =
        "Terminal too small to confirm. Nothing was done; enlarge it and try again.";
    private const string ConfirmClosedText = "Terminal too small to show the confirmation: closed, nothing was done.";

    private readonly KeymapTable _keymap;

    public AppReducer(KeymapTable keymap)
    {
        _keymap = keymap;
    }

    public ReduceResult Reduce(AppState state, Command command)
    {
        var result = StartupReducer.Persisted(
            state,
            ChordEnded(GuardConfirm(state, ReduceCommand(state, command))));
        var settled = StatusMessages.Settle(result.State, TimeOf(command));

        return ReferenceEquals(settled, result.State) ? result : new ReduceResult(settled, result.Effects);
    }

    // The time a command carries (a default time means the sender had none), used to stamp and expire the notice.
    private static DateTimeOffset? TimeOf(Command command)
    {
        var at = command switch
        {
            Tick tick => tick.Now,
            KeyPressed pressed => pressed.At,
            ActionRequested requested => requested.At,
            _ => default,
        };

        return at == default ? null : at;
    }

    /// <summary>Forgets the which-key timing once no keys are pending, so a finished chord leaves no trace.</summary>
    private static ReduceResult ChordEnded(ReduceResult result)
    {
        var state = result.State;

        if (state.PendingKeys.Length > 0 || (state.PendingSince is null && state.WhichKeyOpenFor is null))
        {
            return result;
        }

        return result with { State = state with { PendingSince = null, WhichKeyOpenFor = null } };
    }

    /// <summary>
    /// The Confirm dialog is the only way to answer, so it must be readable: a confirm that would open where the
    /// dialog does not fit is not opened (the state stays as it was and says so), and one that stops fitting after a
    /// resize is closed unanswered with a warning. Nothing runs in either case.
    /// </summary>
    private static ReduceResult GuardConfirm(AppState before, ReduceResult result)
    {
        var after = result.State;

        if (after.Mode != InputMode.Confirm || after.Confirm is null || ConfirmDialog.Fits(after))
        {
            return result;
        }

        if (before.Mode != InputMode.Confirm)
        {
            return Result(StatusMessages.Warn(before, ConfirmTooSmallText));
        }

        var closed = after.Editor is not null ? CommentEditing.KeepEditing(after) : ToNormal(after);

        return Result(StatusMessages.Warn(closed, ConfirmClosedText));
    }

    // A drop on another section of a Scrum backlog asks to move the issues; any other drag selects rows.
    private static ReduceResult RowsDrag(AppState state, RowsDragged dragged)
    {
        return SprintMoveActions.Dropped(state, dragged, dragged.At) ?? SelectionActions.Dragged(state, dragged);
    }

    private ReduceResult ReduceCommand(AppState state, Command command)
    {
        return command switch
        {
            KeyPressed pressed => Followed(state, ReduceKey(state, pressed.Key, pressed.At)),
            ActionRequested requested => Followed(state, ReduceRequested(state, requested)),
            PointerPlaced placed => Followed(state, Pointed(state, placed)),
            RowsDragged dragged => Followed(state, RowsDrag(Unpended(state), dragged)),
            CardDragged dragged => Followed(state, CardDrag.Drop(Unpended(state), dragged)),
            TabActivated activated => ViewReducer.Reduce(Unpended(state), activated),
            OverlayRowClicked clicked => Followed(state, ReduceOverlayRow(state, clicked)),
            ConfirmRequested requested => Result(state with
            {
                Mode = InputMode.Confirm,
                Confirm = requested.Request,
                PendingKeys = string.Empty,
            }),
            TextPasted pasted => Pasted(state, pasted.Text),
            ViewLoadFailed failed => StartupReducer.AfterLoadFailed(state, failed, ViewReducer.Reduce(state, failed)),
            ViewCommand view => ViewReducer.Reduce(state, view),
            FormCommand form => EditorSuggestions.Reply(state, form) ?? FormReducer.Reduce(state, form),
            WriteCommand write => WriteReducer.Reduce(state, write),
            SourceCommand source => SourceReducer.Reduce(state, source),
            StartupRequested => StartupReducer.Begin(Result(state)),
            TabsLoaded loaded => StartupReducer.Loaded(state, loaded),
            TabsSaveFailed failed => Result(StatusMessages.Warn(state, failed.Message)),
            ProfilesChanged changed => ProfileReducer.Changed(state, changed),
            ProfileOperationFailed failed => ProfileReducer.Failed(state, failed),
            ProfileTested tested => ProfileReducer.Tested(state, tested),
            TokenSourcesLoaded sources => Result(state with { TokenSources = sources.Sources }),
            CacheWriteFailed failed => Result(StatusMessages.Warn(state, failed.Message)),
            ConnectionFailed failed => ProfileReducer.ConnectionFailed(state, failed),
            ConnectionCheckRequested => ProfileReducer.CheckConnection(state, announce: false),
            ConnectionChecked connected => ProfileReducer.ConnectionChecked(state, connected),
            ConnectionCheckFailed failed => ProfileReducer.ConnectionCheckFailed(state, failed),
            OpenUrlRequested request => BrowserReducer.Request(state, request),
            UrlOpenFinished finished => BrowserReducer.Finished(state, finished),
            SettingsReloaded reloaded => SettingsReducer.Reloaded(state, reloaded),
            ThemeApplied applied => SettingsReducer.Applied(state, applied),
            KeymapReloaded reloaded => Result(StatusMessages.Keymap(state, reloaded)),
            _ => Result(state),
        };
    }

    private static ReduceResult Pasted(AppState state, string pasted)
    {
        var text = PasteText.FirstLine(pasted);

        if (text.Length > 0 && FormReducer.IsOpen(state))
        {
            return FormReducer.Paste(state, text);
        }

        return Followed(state, Result(Paste(state, pasted)));
    }

    // The editor's suggestion popup follows every key and paste (it opens, narrows or closes with the typing).
    private static ReduceResult Followed(AppState before, ReduceResult result)
    {
        return before.Editor is null ? result : EditorSuggestions.Follow(before, result);
    }

    private static ReduceResult Result(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }

    /// <summary>Quits, but asks first while writes run: their results would be dropped.</summary>
    private static ReduceResult Quit(AppState state)
    {
        if (WriteReducer.RunningText(state) is not { } running)
        {
            return new ReduceResult(state, _quitEffects);
        }

        var request = new ConfirmRequest($"{running}. Quit anyway?", ActionIds.Quit, Label: "Quit");

        return Result(state with { Mode = InputMode.Confirm, Confirm = request, PendingKeys = string.Empty });
    }

    private static string Join(string pending, string key)
    {
        return pending.Length == 0 ? key : $"{pending} {key}";
    }

    // Esc in the body: a picked-up rank issue goes first, then Visual mode, then the marks.
    private static AppState Escaped(AppState state)
    {
        return SelectionActions.Back(state) ?? RankActions.DropPickup(state);
    }

    private static AppState ToNormal(AppState state)
    {
        // The profile manager is a screen: closing the form or the remove question on top of it returns to it.
        var toManager = state.ProfileList is not null && (state.Form is not null || state.Confirm is not null);

        return state with
        {
            Mode = toManager ? InputMode.Insert : InputMode.Normal,
            PendingKeys = string.Empty,
            Count = 0,
            PaletteText = string.Empty,
            PaletteSelected = 0,
            HelpScroll = 0,
            HelpPage = 0,
            HelpFilter = string.Empty,
            HelpFiltering = false,
            MessagesOpen = false,
            MessagesShowSettings = false,
            Confirm = null,
            Form = null,
            Picker = null,
            Jump = null,
            Editor = null,
            ProfileList = toManager ? state.ProfileList : null,
            ContextMenu = null,
        };
    }

    /// <summary>
    /// A click on an overlay row: the row is chosen, then the key's handler runs (Enter takes it, Tab focuses a field,
    /// Ctrl-x removes a profile), so a click and a key end in the same state.
    /// </summary>
    private ReduceResult ReduceOverlayRow(AppState state, OverlayRowClicked clicked)
    {
        var cleared = state with { PendingKeys = string.Empty, Count = 0, Message = null };

        return clicked.Target switch
        {
            MouseTarget.PickerRow when cleared.Picker is { } picker
                => SourceReducer.Accept(cleared with { Picker = picker with { Selected = clicked.Index } }, clicked.At),
            MouseTarget.JumpRow when cleared.Jump is { } jump
                => JumpReducer.Accept(cleared with { Jump = jump with { Selected = clicked.Index } }),
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
            return placement;
        }

        // A mark needs a card under the pointer: a column header only moves the cursor there.
        if (action.Action == ActionIds.SelectionMark && !CardUnderPointer(cleared, placed))
        {
            return placement;
        }

        var acted = ReduceRequested(placement.State, action);

        return new ReduceResult(acted.State, [.. placement.Effects, .. acted.Effects]);
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

    private ReduceResult ReduceRequested(AppState state, ActionRequested request)
    {
        // Cancel is Esc: the guards (discard questions, saving forms, the suggestion popup) live in the key path.
        if (request.Action == ActionIds.Cancel)
        {
            return ReduceKey(state, EscapeKey, request.At);
        }

        var cleared = state with { PendingKeys = string.Empty, Count = 0, Message = null };

        if (SelectionActions.Refusal(cleared, request.Action) is { } refusal)
        {
            return Result(cleared with { Message = refusal });
        }

        if (request.Action == ActionIds.SelectionMark && request.Count == 0)
        {
            // The pointer marks the row it clicked in place; the key marks and moves down.
            return InNormal(cleared, () => SelectionActions.ToggleMark(cleared, false));
        }

        if (request.Count > 1)
        {
            return ApplyCounted(cleared, request.Action, request.Count, request.At);
        }

        return Apply(cleared, request.Action, request.At);
    }

    private ReduceResult ReduceKey(AppState state, string key, DateTimeOffset at)
    {
        var route = InputRouting.Route(state, key, _keymap);

        switch (route.Kind)
        {
            case KeyRouteKind.SuggestionPopup:
                return route.Popup!;
            case KeyRouteKind.Form:
                return FormReducer.Key(state, key, at, _keymap);
            case KeyRouteKind.HelpFilter:
                return Result(HelpReducer.Key(state, key, _keymap));
            case KeyRouteKind.Count:
                return Result(state with { Count = Counts.Append(state.Count, key), Message = null });
            case KeyRouteKind.ChordPrefix:
                var since = state.PendingKeys.Length == 0 ? at : state.PendingSince;

                return Result(state with { PendingKeys = Join(state.PendingKeys, key), PendingSince = since, Message = null });
            case KeyRouteKind.BackOut:
                return ViewReducer.Reduce(state, new IssueDetailBackRequested(state.Session.Active!.Value));
            case KeyRouteKind.Dismiss:
                return Result(StatusMessages.Dismiss(state));
        }

        var cleared = state with { PendingKeys = string.Empty, Count = 0 };
        var resolution = route.Resolution!;

        if (resolution.Action is { } written && SelectionActions.Refusal(cleared, written) is { } refusal)
        {
            return Result(cleared with { Message = refusal });
        }

        if (resolution.Action is { } keyed && ClosedSprintGate.Refuses(cleared, keyed))
        {
            return Result(cleared with { Message = ClosedSprintGate.Refusal });
        }

        if (resolution.Action == ActionIds.IssueUnlink)
        {
            // The only action that needs the time of the key: the confirm carries the write that will start.
            return InNormal(cleared, () => LinkReducer.Unlink(cleared with { Message = null }, at));
        }

        if (route.Kind == KeyRouteKind.Text)
        {
            return Result(TypeText(cleared, key));
        }

        if (state.Count > 0)
        {
            return ApplyCounted(cleared with { Message = null }, resolution.Action!, state.Count, at);
        }

        return Apply(cleared with { Message = null }, resolution.Action!, at);
    }

    /// <summary>
    /// An action typed after a count (<c>docs/ux-keymap.md</c>, "Selection and counts"): a motion or a rank step
    /// repeats; any other write refuses; every other action ignores the count.
    /// </summary>
    private ReduceResult ApplyCounted(AppState state, string action, int count, DateTimeOffset at)
    {
        if (EmptySprintFocus.Refuses(state, action))
        {
            return Result(state with { Message = EmptySprintFocus.Refusal });
        }

        switch (action)
        {
            case ActionIds.NavigateDown:
                return Repeat(state, ListMove.Down, count);
            case ActionIds.NavigateUp:
                return Repeat(state, ListMove.Up, count);
            case ActionIds.RankUp:
                return InNormal(state, () => RankActions.Step(state, RankMove.Up, count, at));
            case ActionIds.RankDown:
                return InNormal(state, () => RankActions.Step(state, RankMove.Down, count, at));
            case ActionIds.RankTop:
            case ActionIds.RankBottom:
            case ActionIds.RankTo:
                return Result(state with { Message = $"A count does not apply to: {ActionCatalog.FindTitle(action)}" });
            default:
                return Apply(state, action, at);
        }
    }

    private ReduceResult Repeat(AppState state, ListMove move, int count)
    {
        if (state.Mode != InputMode.Normal)
        {
            return Navigate(state, move, move == ListMove.Down ? 1 : -1);
        }

        var current = Result(state);

        for (var step = 0; step < count; step++)
        {
            var next = ViewReducer.Navigate(current.State, move);

            current = new ReduceResult(next.State, [.. current.Effects, .. next.Effects]);
        }

        return current;
    }

    /// <summary>
    /// Pasted text goes to the active field as typed text. Only the first line counts (a trailing newline from the
    /// clipboard must not submit anything) and control characters are dropped.
    /// </summary>
    private static AppState Paste(AppState state, string pasted)
    {
        if (state.Mode == InputMode.Insert && state.Editor is not null)
        {
            return CommentEditing.Type(state, PasteText.Lines(pasted));
        }

        var text = PasteText.FirstLine(pasted);

        if (text.Length == 0)
        {
            return state;
        }

        if (state.Mode == InputMode.Insert)
        {
            return TypeInInsert(state, text);
        }

        return state.Mode == InputMode.Command ? WithPaletteText(state, $"{state.PaletteText}{text}") : state;
    }

    private static AppState TypeText(AppState state, string key)
    {
        var text = KeyNotation.ToText(key);

        if (state.Mode == InputMode.Insert && text is not null)
        {
            return TypeInInsert(state, text);
        }

        if (state.Mode != InputMode.Command || text is null)
        {
            return state;
        }

        return WithPaletteText(state, $"{state.PaletteText}{text}");
    }

    // The filter changed, so the highlight goes back to the first row.
    private static AppState WithPaletteText(AppState state, string text)
    {
        return state with { PaletteText = text, PaletteSelected = 0 };
    }

    private static AppState TypeInInsert(AppState state, string text)
    {
        if (state.ContextMenu is { } menu)
        {
            // The menu has no text field; j and k move like in the lists.
            return text switch
            {
                "j" => state with { ContextMenu = ContextMenus.Move(menu, 1) },
                "k" => state with { ContextMenu = ContextMenus.Move(menu, -1) },
                _ => state,
            };
        }

        if (state.Editor is not null)
        {
            return CommentEditing.Type(state, text);
        }

        if (state.Jump is not null)
        {
            return JumpReducer.Type(state, text);
        }

        return state.Picker is null ? ProfileReducer.Type(state, text) : SourceReducer.Type(state, text);
    }

    private ReduceResult Apply(AppState state, string action, DateTimeOffset at)
    {
        if (ClosedSprintGate.Refuses(state, action))
        {
            return Result(state with { Message = ClosedSprintGate.Refusal });
        }

        if (EmptySprintFocus.Refuses(state, action))
        {
            return Result(state with { Message = EmptySprintFocus.Refusal });
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
            ActionIds.StatusHistory => InNormal(state, () => Result(StatusMessages.OpenHistory(state))),
            ActionIds.StatusDismiss => Result(StatusMessages.Dismiss(state)),
            ActionIds.ViewCycleCommandLevel => Result(CycleCommandLevel(state)),
            ActionIds.PaletteOpen => Result(ToNormal(state) with { Mode = InputMode.Command }),
            ActionIds.ContextMenuOpen => InNormal(state, () => OpenContextMenu(state)),
            ActionIds.ViewMenuOpen => InNormal(state, () => OpenViewMenu(state)),
            ActionIds.PaletteBackspace => Result(Backspace(state)),
            ActionIds.PaletteSubmit => SubmitPalette(state, at),
            ActionIds.Cancel => state.Editor is null ? Result(ToNormal(Escaped(state))) : CommentEditing.Cancel(state),
            ActionIds.SelectionVisual => InNormal(state, () => SelectionActions.ToggleVisual(state)),
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
            ActionIds.EditorDelete => Result(CommentEditing.DeleteForward(state)),
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
            ActionIds.ItemOpen => state.Mode == InputMode.Normal ? OpenItem(state) : Result(state),
            ActionIds.SectionNext => InNormal(
                state,
                () => BacklogActions.Jump(state, 1)
                    ?? BoardViewOptions.StepLane(state, 1)
                    ?? DetailActions.Section(state, 1)),
            ActionIds.SectionPrevious => InNormal(
                state,
                () => BacklogActions.Jump(state, -1)
                    ?? BoardViewOptions.StepLane(state, -1)
                    ?? DetailActions.Section(state, -1)),
            ActionIds.SectionToggle => InNormal(state, () => BacklogActions.Toggle(state) ?? SubtaskActions.Toggle(state)
                ?? Result(state)),
            ActionIds.IssueOpenParent => InNormal(state, () => DetailActions.OpenParent(state)),
            ActionIds.IssueTransition => InNormal(state, () => TransitionForms.OpenPicker(state)),
            ActionIds.IssueTransitionNext => InNormal(state, () => TransitionForms.MoveCard(state, 1)),
            ActionIds.IssueTransitionPrevious => InNormal(state, () => TransitionForms.MoveCard(state, -1)),
            ActionIds.TabNext => ViewReducer.StepTab(state, 1),
            ActionIds.TabPrevious => ViewReducer.StepTab(state, -1),
            ActionIds.TabLastActive => ViewReducer.LastActiveTab(state),
            ActionIds.TabNew => SourceReducer.OpenPicker(state),
            ActionIds.TabClose => ViewReducer.CloseActiveTab(state),
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

        return state.Picker is null ? ProfileReducer.Backspace(state) : SourceReducer.Backspace(state);
    }

    private string DropKeys()
    {
        var keys = _keymap.FindKeys(InputMode.Normal, ActionIds.RankTo);

        return keys is null ? "the same key" : string.Concat(keys.Split(' ').Select(KeyText));
    }

    private static string KeyText(string key)
    {
        return key == KeyNotation.Space ? "<space>" : key;
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

    /// <summary>
    /// Help scrolls by <paramref name="helpDelta"/> lines; in Normal mode the list selection moves.
    /// </summary>
    private ReduceResult Navigate(AppState state, ListMove move, int helpDelta)
    {
        if (state is { Mode: InputMode.Help, MessagesOpen: false } && move is ListMove.Down or ListMove.Up)
        {
            return Result(HelpReducer.Select(state, _keymap, helpDelta));
        }

        if (state.Mode == InputMode.Help)
        {
            return Result(ScrollHelp(state, helpDelta));
        }

        return state.Mode == InputMode.Normal ? ViewReducer.Navigate(state, move) : Result(state);
    }

    private static ReduceResult ToggleLayout(AppState state)
    {
        return state.Mode == InputMode.Normal ? ViewReducer.ToggleLayout(state) : Result(state);
    }

    private static ReduceResult NavigateColumn(AppState state, BoardColumnMove move)
    {
        return state.Mode == InputMode.Normal ? ViewReducer.NavigateColumn(state, move) : Result(state);
    }

    private static AppState ToggleHelp(AppState state)
    {
        if (state.Mode == InputMode.Help)
        {
            return ToNormal(state);
        }

        return ToNormal(state) with { Mode = InputMode.Help };
    }

    /// <summary>
    /// Switches the command level for the session (not saved; <c>:set commandlevel</c> saves) and says which level is
    /// on. The help scroll stays inside the shorter or longer help.
    /// </summary>
    private AppState CycleCommandLevel(AppState state)
    {
        var level = state.Settings.CommandLevel == CommandLevel.Everyday
            ? CommandLevel.Advanced
            : CommandLevel.Everyday;
        var changed = state with
        {
            Settings = state.Settings with { CommandLevel = level },
            Message = $"Command level: {level.ToString().ToLowerInvariant()}",
        };

        return changed with { HelpScroll = Math.Clamp(changed.HelpScroll, 0, Math.Max(0, LineCount(changed) - 1)) };
    }

    private int LineCount(AppState state)
    {
        return state.MessagesOpen
            ? StatusMessages.OverlayRowCount(state)
            : HelpPages.Lines(state, _keymap).Count;
    }

    private AppState ScrollHelp(AppState state, int delta)
    {
        if (state.Mode != InputMode.Help)
        {
            return state;
        }

        return state with { HelpScroll = Math.Clamp(state.HelpScroll + delta, 0, Math.Max(0, LineCount(state) - 1)) };
    }

    /// <summary>Opens the right-click menu for the cursor; says so when there is no issue under it.</summary>
    private static ReduceResult OpenContextMenu(AppState state)
    {
        if (ContextMenus.For(state) is not { } menu)
        {
            return Result(state with { Message = "No issue selected." });
        }

        return Result(ToNormal(state) with { Mode = InputMode.Insert, ContextMenu = menu });
    }

    /// <summary>Opens the menu of the tab and the view (not of an issue); says so when there is no tab.</summary>
    private static ReduceResult OpenViewMenu(AppState state)
    {
        if (ContextMenus.ForView(state) is not { } menu)
        {
            return Result(state with { Message = "No tab open." });
        }

        return Result(ToNormal(state) with { Mode = InputMode.Insert, ContextMenu = menu });
    }

    // Closes the menu, then runs the item like its key: the same handler, so delete and unlink still ask first.
    private ReduceResult RunMenuItem(AppState state, ContextMenuItem item, DateTimeOffset at)
    {
        return ReduceRequested(ToNormal(state), new ActionRequested(item.Action, 1, at));
    }

    private static AppState Backspace(AppState state)
    {
        if (state.PaletteText.Length == 0)
        {
            return ToNormal(state);
        }

        return WithPaletteText(state, TextElements.RemoveLast(state.PaletteText));
    }

    private AppState MovePalette(AppState state, int delta)
    {
        var count = PaletteRows.Selectable(state, _keymap).Count;

        return state with { PaletteSelected = Math.Clamp(state.PaletteSelected + delta, 0, Math.Max(0, count - 1)) };
    }

    /// <summary>
    /// A click on a palette row chooses it and runs it like <c>Enter</c>; the wheel over the rows moves the
    /// highlight (<see cref="DefaultMouseMap.WheelRows"/> rows) and runs nothing.
    /// </summary>
    private ReduceResult ClickPaletteRow(AppState state, OverlayRowClicked clicked)
    {
        if (clicked.Action == ActionIds.PickerNext)
        {
            return Result(MovePalette(state, DefaultMouseMap.WheelRows));
        }

        if (clicked.Action == ActionIds.PickerPrevious)
        {
            return Result(MovePalette(state, -DefaultMouseMap.WheelRows));
        }

        return RunPaletteRow(state with { PaletteSelected = clicked.Index }, clicked.At);
    }

    /// <summary>
    /// Enter in the palette: text that starts with a command name runs as a command line (as it always did);
    /// otherwise the highlighted row runs, after the palette closed, exactly as its key would. Text that matches no
    /// row is an unknown command.
    /// </summary>
    private ReduceResult SubmitPalette(AppState state, DateTimeOffset at)
    {
        var text = state.PaletteText.Trim();

        if (text.Length > 0 && (PaletteRows.IsCommandLine(text) || PaletteRows.Selectable(state, _keymap).Count == 0))
        {
            return RunCommandLine(state, at);
        }

        return RunPaletteRow(state, at);
    }

    private ReduceResult RunPaletteRow(AppState state, DateTimeOffset at)
    {
        var rows = PaletteRows.Selectable(state, _keymap);
        var normal = ToNormal(state);

        if (rows.Count == 0)
        {
            return Result(normal);
        }

        var row = rows[Math.Clamp(state.PaletteSelected, 0, rows.Count - 1)];

        if (!row.Enabled)
        {
            return Result(normal with { Message = $"Not available yet: {row.Title}" });
        }

        if (row.Kind == PaletteRowKind.Command)
        {
            return RunCommandLine(state with { PaletteText = row.Id }, at);
        }

        return ReduceRequested(normal, new ActionRequested(row.Id, 1, at));
    }

    /// <summary><c>view.saved-save</c>: the command line with <c>view save </c> typed, ready for the name.</summary>
    private static ReduceResult OpenSaveCommand(AppState state)
    {
        var command = ToNormal(state) with { Mode = InputMode.Command, PaletteText = $"{PaletteCommands.View} save " };

        return Result(command);
    }

    private static ReduceResult RunCommandLine(AppState state, DateTimeOffset at)
    {
        var text = state.PaletteText.Trim();
        var normal = ToNormal(state);

        if (ClosedSprintGate.RefusesCommand(state, text))
        {
            return Result(normal with { Message = ClosedSprintGate.Refusal });
        }

        if (EmptySprintFocus.RefusesCommand(state, text))
        {
            return Result(normal with { Message = EmptySprintFocus.Refusal });
        }

        return text switch
        {
            "" => Result(normal),
            "q" or "quit" => Quit(normal),
            "help" or "h" => Result(normal with { Mode = InputMode.Help }),
            PaletteCommands.History + " clear" => SourceReducer.ClearHistory(normal),
            PaletteCommands.Status => TransitionForms.OpenPicker(normal),
            "status next" => TransitionForms.MoveCard(normal, 1),
            "status prev" or "status previous" => TransitionForms.MoveCard(normal, -1),
            PaletteCommands.Labels => FormReducer.Reduce(normal, new OpenLabelsFormRequested()),
            PaletteCommands.Create => FormReducer.Reduce(normal, new OpenCreateFormRequested(false)),
            PaletteCommands.CreateSubtask => FormReducer.Reduce(normal, new OpenCreateFormRequested(true)),
            PaletteCommands.Edit => FormOpener.Edit(normal),
            PaletteCommands.Priority => FormOpener.Priority(normal),
            PaletteCommands.Estimate => FormOpener.Estimate(normal),
            PaletteCommands.Assign => FormOpener.Assign(normal),
            PaletteCommands.AssignMe => FormOpener.AssignMe(normal, at),
            _ when IsLayoutCommand(text) => LayoutCommand(normal, text["layout".Length..].Trim()),
            _ when IsQuickFilterCommand(text) => BoardViewOptions.FilterCommand(
                normal,
                text["quickfilter".Length..].Trim()),
            _ when SourceVerb(text) is { } verb => SourceReducer.Palette(normal, verb, text[verb.Length..]),
            _ when IsBrowseCommand(text) => BrowserReducer.OpenIssue(normal, BrowseKey(text)),
            _ when IsTabCommand(text) => TabCommand(normal, text["tab".Length..].Trim()),
            _ when IsSetCommand(text) => SettingsReducer.Palette(normal, text["set".Length..]),
            _ when IsThemeCommand(text) => SettingsReducer.Theme(normal, text[PaletteCommands.Theme.Length..].Trim()),
            _ when IsProfileCommand(text) => ProfileReducer.Palette(normal, text["profile".Length..]),
            _ when IsKeysCommand(text) => KeysCommand(normal, text[PaletteCommands.Keys.Length..].Trim()),
            _ => Result(normal with { Message = $"Unknown command: {text}", MessageSeverity = StatusSeverity.Warning }),
        };
    }

    /// <summary>The source command the text starts with (<c>jql</c>, <c>filter</c>, ...), or null.</summary>
    private static string? SourceVerb(string text)
    {
        foreach (var verb in _sourceVerbs)
        {
            if (text == verb || text.StartsWith($"{verb} ", StringComparison.Ordinal))
            {
                return verb;
            }
        }

        return null;
    }

    private static bool IsBrowseCommand(string text)
    {
        return text == "browse" || text.StartsWith("browse ", StringComparison.Ordinal);
    }

    /// <summary>The typed key of <c>:browse KEY</c>, upper-cased; null when none was typed.</summary>
    private static string? BrowseKey(string text)
    {
        var key = text["browse".Length..].Trim().ToUpperInvariant();

        return key.Length == 0 ? null : key;
    }

    private static bool IsQuickFilterCommand(string text)
    {
        return text == "quickfilter" || text.StartsWith("quickfilter ", StringComparison.Ordinal);
    }

    private static bool IsLayoutCommand(string text)
    {
        return text == "layout" || text.StartsWith("layout ", StringComparison.Ordinal);
    }

    /// <summary><c>:layout list|columns</c>: the same handler as <c>view.layout-toggle</c>.</summary>
    private static ReduceResult LayoutCommand(AppState state, string argument)
    {
        return argument switch
        {
            "list" => ViewReducer.SetLayout(state, ViewLayout.List),
            "columns" => ViewReducer.SetLayout(state, ViewLayout.Columns),
            _ => Result(state with { Message = "Usage: :layout list|columns" }),
        };
    }

    private static bool IsTabCommand(string text)
    {
        return text == "tab" || text.StartsWith("tab ", StringComparison.Ordinal);
    }

    /// <summary><c>:tab new|close|next|prev|&lt;n&gt;</c>: the same handlers as the tab keys.</summary>
    private static ReduceResult TabCommand(AppState state, string argument)
    {
        if (argument == "new")
        {
            return SourceReducer.OpenPicker(state);
        }

        if (argument == "close")
        {
            return ViewReducer.CloseActiveTab(state);
        }

        if (argument is "next" or "prev" or "previous")
        {
            return ViewReducer.StepTab(state, argument == "next" ? 1 : -1);
        }

        if (int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return ViewReducer.GoToTab(state, number);
        }

        return Result(state with { Message = "Usage: :tab new|close|next|prev|<n>" });
    }

    private static bool IsThemeCommand(string text)
    {
        return text == PaletteCommands.Theme || text.StartsWith($"{PaletteCommands.Theme} ", StringComparison.Ordinal);
    }

    private static bool IsSetCommand(string text)
    {
        return text == "set" || text.StartsWith("set ", StringComparison.Ordinal);
    }

    private static ReduceResult KeysCommand(AppState state, string argument)
    {
        if (argument == "reload")
        {
            return new ReduceResult(state, [new ReloadKeymapEffect()]);
        }

        return Result(state with { Message = $"Usage: :{PaletteCommands.UsageOf(PaletteCommands.Keys)}" });
    }

    private static bool IsKeysCommand(string text)
    {
        return text == PaletteCommands.Keys || text.StartsWith($"{PaletteCommands.Keys} ", StringComparison.Ordinal);
    }

    private static bool IsProfileCommand(string text)
    {
        return text == "profile" || text.StartsWith("profile ", StringComparison.Ordinal);
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
