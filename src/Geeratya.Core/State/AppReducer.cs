using System.Globalization;
using Geeratya.Core.Keymap;

namespace Geeratya.Core.State;

/// <summary>
/// <c>(State, Command) -> (State, Effects)</c>. Holds only the keymap; no clock, no I/O, no terminal.
/// Esc and q follow <c>docs/ux.md</c>: Esc backs out one level, q quits only from Normal mode.
/// </summary>
public sealed class AppReducer
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];
    private static readonly IReadOnlyList<Effect> _quitEffects = [new QuitEffect()];

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
            ProfilesChanged changed => ProfileReducer.Changed(state, changed),
            ProfileOperationFailed failed => ProfileReducer.Failed(state, failed),
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

    private static AppState TypeText(AppState state, string key)
    {
        var text = KeyNotation.ToText(key);

        if (state.Mode == InputMode.Insert && text is not null)
        {
            return ProfileReducer.Type(state, text);
        }

        if (state.Mode != InputMode.Command || text is null)
        {
            return state;
        }

        return state with { PaletteText = $"{state.PaletteText}{text}" };
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
            ActionIds.FormNext => ProfileReducer.Submit(state),
            ActionIds.FormBackspace => Result(ProfileReducer.Backspace(state)),
            ActionIds.NavigateDown => Result(ScrollHelp(state, 1)),
            ActionIds.NavigateUp => Result(ScrollHelp(state, -1)),
            _ => Result(state),
        };
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
            _ when IsProfileCommand(text) => ProfileReducer.Palette(normal, text["profile".Length..]),
            _ => Result(normal with { Message = $"Unknown command: {text}" }),
        };
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
