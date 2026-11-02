using System.Globalization;
using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Sources;
using Geeratya.Core.Views;

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
    private readonly int _helpLineCount;

    public AppReducer(KeymapTable keymap)
    {
        _keymap = keymap;
        _helpLineCount = KeymapDocument.RenderHelpLines(keymap).Count;
    }

    public ReduceResult Reduce(AppState state, Command command)
    {
        return command switch
        {
            KeyPressed pressed => ReduceKey(state, pressed.Key),
            ConfirmRequested requested => Result(state with
            {
                Mode = InputMode.Confirm,
                Confirm = requested.Request,
                PendingKeys = string.Empty,
            }),
            TextPasted pasted => Result(Paste(state, pasted.Text)),
            ViewCommand view => ViewReducer.Reduce(state, view),
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
            _ => Result(state),
        };
    }

    private static ReduceResult Result(AppState state)
    {
        return new ReduceResult(state, _noEffects);
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
            PaletteText = string.Empty,
            HelpScroll = 0,
            Confirm = null,
            Form = null,
            Picker = null,
        };
    }

    private ReduceResult ReduceKey(AppState state, string key)
    {
        var resolution = _keymap.Resolve(state.Mode, state.PendingKeys, key);

        if (resolution.Kind == KeyResolutionKind.Prefix)
        {
            return Result(state with { PendingKeys = Join(state.PendingKeys, key), Message = null });
        }

        var cleared = state with { PendingKeys = string.Empty };

        if (resolution.Kind == KeyResolutionKind.None)
        {
            return Result(TypeText(cleared, key));
        }

        return Apply(cleared with { Message = null }, resolution.Action!);
    }

    /// <summary>
    /// Pasted text goes to the active field as typed text. Only the first line counts (a trailing newline from the
    /// clipboard must not submit anything) and control characters are dropped.
    /// </summary>
    private static AppState Paste(AppState state, string pasted)
    {
        var text = PasteText.FirstLine(pasted);

        if (text.Length == 0)
        {
            return state;
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
        return state.Picker is null ? ProfileReducer.Type(state, text) : SourceReducer.Type(state, text);
    }

    private ReduceResult Apply(AppState state, string action)
    {
        return action switch
        {
            ActionIds.Quit => new ReduceResult(state, _quitEffects),
            ActionIds.HelpToggle => Result(ToggleHelp(state)),
            ActionIds.HelpClose => Result(ToNormal(state)),
            ActionIds.PaletteOpen => Result(ToNormal(state) with { Mode = InputMode.Command }),
            ActionIds.PaletteBackspace => Result(Backspace(state)),
            ActionIds.PaletteSubmit => SubmitPalette(state),
            ActionIds.Cancel => Result(ToNormal(state)),
            ActionIds.ConfirmNo => Result(ToNormal(state) with { Message = "Cancelled." }),
            ActionIds.ConfirmYes => Confirmed(state),
            ActionIds.FormNext => state.Picker is null ? ProfileReducer.Submit(state) : SourceReducer.Accept(state),
            ActionIds.FormBackspace => Result(Erase(state)),
            ActionIds.PickerNext => Result(SourceReducer.Move(state, 1)),
            ActionIds.PickerPrevious => Result(SourceReducer.Move(state, -1)),
            ActionIds.NavigateDown => Navigate(state, ListMove.Down, 1),
            ActionIds.NavigateUp => Navigate(state, ListMove.Up, -1),
            ActionIds.NavigateTop => Navigate(state, ListMove.Top, 0),
            ActionIds.NavigateBottom => Navigate(state, ListMove.Bottom, 0),
            ActionIds.NavigatePageDown => Navigate(state, ListMove.PageDown, 0),
            ActionIds.NavigatePageUp => Navigate(state, ListMove.PageUp, 0),
            ActionIds.TabNext => ViewReducer.StepTab(state, 1),
            ActionIds.TabPrevious => ViewReducer.StepTab(state, -1),
            ActionIds.TabNew => SourceReducer.OpenPicker(state),
            ActionIds.TabClose => ViewReducer.CloseActiveTab(state),
            ActionIds.ViewRefresh => ViewReducer.RefreshActive(state),
            _ when TabNumber(action) is { } number => ViewReducer.GoToTab(state, number),
            _ => Result(state),
        };
    }

    private static AppState Erase(AppState state)
    {
        return state.Picker is null ? ProfileReducer.Backspace(state) : SourceReducer.Backspace(state);
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

        return state with { HelpScroll = Math.Clamp(state.HelpScroll + delta, 0, Math.Max(0, _helpLineCount - 1)) };
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
            "q" or "quit" => new ReduceResult(normal, _quitEffects),
            "help" or "h" => Result(normal with { Mode = InputMode.Help }),
            _ when SourceVerb(text) is { } verb => SourceReducer.Palette(normal, verb, text[verb.Length..]),
            _ when IsTabCommand(text) => TabCommand(normal, text["tab".Length..].Trim()),
            _ when IsProfileCommand(text) => ProfileReducer.Palette(normal, text["profile".Length..]),
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

        return Apply(normal, request.ActionOnConfirm);
    }
}
