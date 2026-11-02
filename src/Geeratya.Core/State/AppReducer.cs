using System.Globalization;
using Geeratya.Core.Boards;
using Geeratya.Core.Cache;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Sources;
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
    ];

    private readonly KeymapTable _keymap;

    public AppReducer(KeymapTable keymap)
    {
        _keymap = keymap;
    }

    public ReduceResult Reduce(AppState state, Command command)
    {
        return command switch
        {
            KeyPressed pressed => ReduceKey(state, pressed.Key, pressed.At),
            ConfirmRequested requested => Result(state with
            {
                Mode = InputMode.Confirm,
                Confirm = requested.Request,
                PendingKeys = string.Empty,
            }),
            TextPasted pasted => Result(Paste(state, pasted.Text)),
            ViewCommand view => ViewReducer.Reduce(state, view),
            FormCommand form => FormReducer.Reduce(state, form),
            WriteCommand write => WriteReducer.Reduce(state, write),
            SourceCommand source => SourceReducer.Reduce(state, source),
            ProfilesChanged changed => ProfileReducer.Changed(state, changed),
            ProfileOperationFailed failed => ProfileReducer.Failed(state, failed),
            CacheWriteFailed failed => Result(state with { Message = failed.Message }),
            ConnectionFailed failed => ProfileReducer.ConnectionFailed(state, failed),
            ConnectionCheckRequested => ProfileReducer.CheckConnection(state, announce: false),
            ConnectionChecked connected => ProfileReducer.ConnectionChecked(state, connected),
            ConnectionCheckFailed failed => ProfileReducer.ConnectionCheckFailed(state, failed),
            OpenUrlRequested request => BrowserReducer.Request(state, request),
            UrlOpenFinished finished => BrowserReducer.Finished(state, finished),
            KeymapReloaded reloaded => Result(state with { Message = reloaded.Message }),
            _ => Result(state),
        };
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

        var request = new ConfirmRequest($"{running}. Quit anyway?", ActionIds.Quit);

        return Result(state with { Mode = InputMode.Confirm, Confirm = request, PendingKeys = string.Empty });
    }

    private static string Join(string pending, string key)
    {
        return pending.Length == 0 ? key : $"{pending} {key}";
    }

    private static AppState ToNormal(AppState state)
    {
        return state with
        {
            Mode = InputMode.Normal,
            PendingKeys = string.Empty,
            Count = 0,
            PaletteText = string.Empty,
            HelpScroll = 0,
            Confirm = null,
            Form = null,
            Picker = null,
            Editor = null,
        };
    }

    private ReduceResult ReduceKey(AppState state, string key, DateTimeOffset at)
    {
        if (FormReducer.IsOpen(state))
        {
            return FormReducer.Key(state, key, at, _keymap);
        }

        if (Counts.IsDigit(state, key))
        {
            return Result(state with { Count = Counts.Append(state.Count, key), Message = null });
        }

        var resolution = _keymap.Resolve(state.Mode, state.PendingKeys, key);

        if (resolution.Kind == KeyResolutionKind.Prefix)
        {
            return Result(state with { PendingKeys = Join(state.PendingKeys, key), Message = null });
        }

        if (IsBackOut(state, resolution))
        {
            return ViewReducer.Reduce(state, new IssueDetailBackRequested(state.Session.Active!.Value));
        }

        var cleared = state with { PendingKeys = string.Empty, Count = 0 };

        if (resolution.Kind == KeyResolutionKind.None)
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
    /// Esc in Normal mode with nothing typed and no notice to clear pops the issue stack of the active tab
    /// (<c>docs/ux.md</c>, "Focus model and key style").
    /// </summary>
    private static bool IsBackOut(AppState state, KeyResolution resolution)
    {
        return resolution.Kind == KeyResolutionKind.Action
            && resolution.Action == ActionIds.Cancel
            && state.Mode == InputMode.Normal
            && state.PendingKeys.Length == 0
            && state.Message is null
            && ViewReducer.CanGoBack(state);
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

        if (FormReducer.IsOpen(state))
        {
            return FormReducer.Paste(state, text);
        }

        if (state.Mode == InputMode.Insert)
        {
            return TypeInInsert(state, text);
        }

        return state.Mode == InputMode.Command ? state with { PaletteText = $"{state.PaletteText}{text}" } : state;
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

        return state with { PaletteText = $"{state.PaletteText}{text}" };
    }

    private static AppState TypeInInsert(AppState state, string text)
    {
        if (state.Editor is not null)
        {
            return CommentEditing.Type(state, text);
        }

        return state.Picker is null ? ProfileReducer.Type(state, text) : SourceReducer.Type(state, text);
    }

    private ReduceResult Apply(AppState state, string action, DateTimeOffset at = default)
    {
        return action switch
        {
            ActionIds.Quit => Quit(state),
            ActionIds.HelpToggle => Result(ToggleHelp(state)),
            ActionIds.HelpClose => Result(ToNormal(state)),
            ActionIds.PaletteOpen => Result(ToNormal(state) with { Mode = InputMode.Command }),
            ActionIds.PaletteBackspace => Result(Backspace(state)),
            ActionIds.PaletteSubmit => SubmitPalette(state),
            ActionIds.Cancel => state.Editor is null ? Result(ToNormal(RankActions.DropPickup(state))) : CommentEditing.Cancel(state),
            ActionIds.RankUp => InNormal(state, () => RankActions.Step(state, RankMove.Up, 1, at)),
            ActionIds.RankDown => InNormal(state, () => RankActions.Step(state, RankMove.Down, 1, at)),
            ActionIds.RankTop => InNormal(state, () => RankActions.Step(state, RankMove.Top, 1, at)),
            ActionIds.RankBottom => InNormal(state, () => RankActions.Step(state, RankMove.Bottom, 1, at)),
            ActionIds.RankTo => InNormal(state, () => RankActions.PickUpOrDrop(state, DropKeys(), at)),
            ActionIds.ConfirmNo => DeclineConfirm(state),
            ActionIds.ConfirmYes => Confirmed(state),
            ActionIds.FormNext => Accept(state),
            ActionIds.FormBackspace => Result(Erase(state)),
            ActionIds.PickerNext => Result(MovePicker(state, 1)),
            ActionIds.PickerPrevious => Result(MovePicker(state, -1)),
            ActionIds.FormSubmit => CommentEditing.Save(state, at),
            ActionIds.EditorLeft => Result(CommentEditing.Move(state, EditorMove.Left)),
            ActionIds.EditorRight => Result(CommentEditing.Move(state, EditorMove.Right)),
            ActionIds.EditorRowStart => Result(CommentEditing.Move(state, EditorMove.RowStart)),
            ActionIds.EditorRowEnd => Result(CommentEditing.Move(state, EditorMove.RowEnd)),
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
            ActionIds.ItemOpen => state.Mode == InputMode.Normal ? OpenItem(state) : Result(state),
            ActionIds.SectionNext => InNormal(state, () => DetailActions.Section(state, 1)),
            ActionIds.SectionPrevious => InNormal(state, () => DetailActions.Section(state, -1)),
            ActionIds.IssueOpenParent => InNormal(state, () => DetailActions.OpenParent(state)),
            ActionIds.IssueTransition => InNormal(state, () => TransitionForms.OpenPicker(state)),
            ActionIds.IssueTransitionNext => InNormal(state, () => TransitionForms.MoveCard(state, 1)),
            ActionIds.IssueTransitionPrevious => InNormal(state, () => TransitionForms.MoveCard(state, -1)),
            ActionIds.TabNext => ViewReducer.StepTab(state, 1),
            ActionIds.TabPrevious => ViewReducer.StepTab(state, -1),
            ActionIds.TabNew => SourceReducer.OpenPicker(state),
            ActionIds.TabClose => ViewReducer.CloseActiveTab(state),
            ActionIds.IssueDelete => InNormal(state, () => DeleteReducer.Start(state)),
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
        return DetailActions.EnterBrowserRow(state) ?? ViewReducer.OpenSelected(state);
    }

    private static ReduceResult InNormal(AppState state, Func<ReduceResult> action)
    {
        return state.Mode == InputMode.Normal ? action() : Result(state);
    }

    /// <summary>Enter in Insert mode: a new line in the editor, else accepts the form field or picker row.</summary>
    private static ReduceResult Accept(AppState state)
    {
        if (state.Editor is not null)
        {
            return Result(CommentEditing.Type(state, "\n"));
        }

        return state.Picker is null ? ProfileReducer.Submit(state) : SourceReducer.Accept(state);
    }

    /// <summary>Up and down in Insert mode: a row up or down in the editor, else the picker selection.</summary>
    private static AppState MovePicker(AppState state, int direction)
    {
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

        return Result(ToNormal(state) with { Message = "Cancelled." });
    }

    private static AppState Erase(AppState state)
    {
        if (state.Editor is not null)
        {
            return CommentEditing.Backspace(state);
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

    private AppState ScrollHelp(AppState state, int delta)
    {
        if (state.Mode != InputMode.Help)
        {
            return state;
        }

        return state with { HelpScroll = Math.Clamp(state.HelpScroll + delta, 0, Math.Max(0, KeymapDocument.RenderHelpLines(_keymap).Count - 1)) };
    }

    private static AppState Backspace(AppState state)
    {
        if (state.PaletteText.Length == 0)
        {
            return ToNormal(state);
        }

        return state with { PaletteText = RemoveLastTextElement(state.PaletteText) };
    }

    private static ReduceResult SubmitPalette(AppState state)
    {
        var text = state.PaletteText.Trim();
        var normal = ToNormal(state);

        return text switch
        {
            "" => Result(normal),
            "q" or "quit" => Quit(normal),
            "help" or "h" => Result(normal with { Mode = InputMode.Help }),
            PaletteCommands.Status => TransitionForms.OpenPicker(normal),
            "status next" => TransitionForms.MoveCard(normal, 1),
            "status prev" or "status previous" => TransitionForms.MoveCard(normal, -1),
            PaletteCommands.Labels => FormReducer.Reduce(normal, new OpenLabelsFormRequested()),
            _ when IsLayoutCommand(text) => LayoutCommand(normal, text["layout".Length..].Trim()),
            _ when SourceVerb(text) is { } verb => SourceReducer.Palette(normal, verb, text[verb.Length..]),
            _ when IsBrowseCommand(text) => BrowserReducer.OpenIssue(normal, BrowseKey(text)),
            _ when IsTabCommand(text) => TabCommand(normal, text["tab".Length..].Trim()),
            _ when IsProfileCommand(text) => ProfileReducer.Palette(normal, text["profile".Length..]),
            _ when IsKeysCommand(text) => KeysCommand(normal, text[PaletteCommands.Keys.Length..].Trim()),
            _ => Result(normal with { Message = $"Unknown command: {text}" }),
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

    private static string RemoveLastTextElement(string text)
    {
        var starts = StringInfo.ParseCombiningCharacters(text);

        return text[..starts[^1]];
    }

    private ReduceResult Confirmed(AppState state)
    {
        var request = state.Confirm;
        var normal = ToNormal(state);

        if (request is null)
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
            : Apply(normal, request.ActionOnConfirm);
    }
}
