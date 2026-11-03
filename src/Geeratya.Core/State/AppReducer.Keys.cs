using Geeratya.Core.Backlog;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Help;
using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Links;
using Geeratya.Core.Lists;
using Geeratya.Core.Selection;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;

namespace Geeratya.Core.State;

public sealed partial class AppReducer
{
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

        if (resolution.Action == ActionIds.IssueUnlink && state.SettingsScreen is null)
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
    /// repeats (so do the help scroll and the overlay highlight moves of a wheel notch); any other write refuses; every other action ignores the count.
    /// </summary>
    private ReduceResult ApplyCounted(AppState state, string action, int count, DateTimeOffset at)
    {
        if (SettingsScreenReducer.Handle(state, action) is { } onScreen)
        {
            return onScreen;
        }

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
            case ActionIds.HelpScrollDown:
                return Result(ScrollHelp(state, count));
            case ActionIds.HelpScrollUp:
                return Result(ScrollHelp(state, -count));
            case ActionIds.PickerNext:
                return Result(MovePicker(state, count));
            case ActionIds.PickerPrevious:
                return Result(MovePicker(state, -count));
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

        return state.Mode == InputMode.Command ? PromptEditing.Insert(state, text) : state;
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

        return PromptEditing.Insert(state, text);
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
}
