using System.Globalization;
using Geeratya.Core.Backlog;
using Geeratya.Core.CommandPalette;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.Views;

namespace Geeratya.Core.State;

public sealed partial class AppReducer
{
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
            return RunCommandLine(state with { PaletteText = row.Id, PaletteBack = 0 }, at);
        }

        return ReduceRequested(normal, new ActionRequested(row.Id, 1, at));
    }

    /// <summary><c>view.saved-save</c>: the command line with <c>view save </c> typed, ready for the name.</summary>
    private static ReduceResult OpenSaveCommand(AppState state)
    {
        var command = ToNormal(state) with { Mode = InputMode.Command, PaletteText = $"{PaletteCommands.View} save ", PaletteBack = 0 };

        return Result(command);
    }

    /// <summary><c>tab.rename</c>: the command line with <c>tab rename </c> and the current name typed.</summary>
    private static ReduceResult OpenRenameCommand(AppState state)
    {
        if (TabRenameReducer.ActiveTab(state) is not { } tab)
        {
            return TabRenameReducer.NoTab(state);
        }

        var text = TabRenameReducer.CommandFor(tab);

        return Result(ToNormal(state) with { Mode = InputMode.Command, PaletteText = text, PaletteBack = 0 });
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
            PaletteCommands.Settings => SettingsScreenReducer.Toggle(normal),
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

    /// <summary><c>:tab new|rename [name]|close|next|prev|&lt;n&gt;</c>: the same handlers as the tab keys.</summary>
    private static ReduceResult TabCommand(AppState state, string argument)
    {
        if (argument == "new")
        {
            return SourceReducer.OpenPicker(state);
        }

        if (argument == "rename" || argument.StartsWith("rename ", StringComparison.Ordinal))
        {
            return TabRenameReducer.Rename(state, argument["rename".Length..]);
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

        return Result(state with { Message = "Usage: :tab new|rename [name]|close|next|prev|<n>" });
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
}
