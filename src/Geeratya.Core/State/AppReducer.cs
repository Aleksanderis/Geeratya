using Geeratya.Core.Cache;
using Geeratya.Core.Comments;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Selection;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.Startup;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;

namespace Geeratya.Core.State;

/// <summary>
/// <c>(State, Command) -> (State, Effects)</c>. Holds only the keymap; no clock, no I/O, no terminal.
/// Esc and q follow <c>docs/ux.md</c>: Esc backs out one level, q quits only from Normal mode.
/// </summary>
public sealed partial class AppReducer
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];
    private static readonly IReadOnlyList<Effect> _quitEffects = [new QuitEffect()];

    // A click on a sub-task row runs the key's handler (mouse table: SubtaskRow).
    private static readonly ActionRequested _subtaskClick = new(ActionIds.SectionToggle, 1, DateTimeOffset.UnixEpoch);

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
    private const string SettingsResetHint = "Open the settings screen first (:settings).";
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
        var reduced = SettingsScreenReducer.RestorePreview(state, ReduceCommand(state, command));
        var result = StartupReducer.Persisted(
            state,
            ChordEnded(GuardConfirm(state, ProfileReducer.ManagerClosed(state, reduced))));
        var settled = StatusMessages.Settle(result.State, TimeOf(command));

        return new ReduceResult(settled, WithCutoff(result.Effects, settled));
    }

    /// <summary>
    /// Stamps the Done cutoff inputs on every view load, whichever path made it (open, refresh tick, next page, tab
    /// switch): the setting <c>board.done-cutoff</c> and the quick choice of the load's tab, so the host needs no
    /// settings or state access.
    /// </summary>
    private static IReadOnlyList<Effect> WithCutoff(IReadOnlyList<Effect> effects, AppState state)
    {
        if (!effects.Any(effect => effect is LoadViewPageEffect))
        {
            return effects;
        }

        return [.. effects.Select(effect => effect is LoadViewPageEffect load ? Stamped(load, state) : effect)];
    }

    private static LoadViewPageEffect Stamped(LoadViewPageEffect load, AppState state)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, load.Tab);

        return load with { DoneCutoffDays = state.Settings.DoneCutoffDays, DoneCutoffChoice = tab?.DoneCutoffChoice };
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
        return SprintMoveActions.Dropped(state, dragged, dragged.At)
            ?? (dragged.Live ? SelectionActions.DragEnded(state, dragged) : SelectionActions.Dragged(state, dragged));
    }

    private ReduceResult ReduceCommand(AppState state, Command command)
    {
        return command switch
        {
            KeyPressed pressed => Followed(state, ReduceKey(state, pressed.Key, pressed.At)),
            ActionRequested requested => Followed(state, ReduceRequested(state, requested)),
            TabHovered hovered => Result(hovered.Tab == state.HoveredTab ? state : state with { HoveredTab = hovered.Tab }),
            PointerPlaced placed => Followed(state, Pointed(state, placed)),
            RowsDragged dragged => Followed(state, RowsDrag(Unpended(state), dragged)),
            RowsDragging dragging => Followed(state, SelectionActions.Dragging(Unpended(state), dragging)),
            RowsDragScrolled scrolled => Followed(state, SelectionActions.DragScrolled(state, scrolled)),
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
            CacheProblem failed => Result(StatusMessages.AddCacheProblem(state, failed.Message)),
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
}
