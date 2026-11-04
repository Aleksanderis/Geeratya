using Geeratya.Core.Boards;
using Geeratya.Core.CommandPalette;
using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
using Geeratya.Core.State;

namespace Geeratya.Core.Views;

/// <summary>
/// The quick picker of a Kanban board's Done cutoff (<c>view.done-cutoff</c>, <c>:donecutoff</c>): Jira's choices
/// (1 to 4 weeks, all) plus "follow Jira". The choice belongs to the tab and is kept with the remembered tab (so per
/// profile and board); it is never written to Jira. The key opens the command palette on the rows below, the command
/// takes the choice as an argument. Pure.
/// </summary>
public static class DoneCutoffPicker
{
    public const string NoBoardText = "The Done cutoff belongs to a Kanban board.";

    /// <summary>What the palette is filtered by when the picker opens: the prefix of every row title.</summary>
    public const string Prefix = "Done cutoff:";

    private const string AutoWord = "auto";
    private const string Usage = "Usage: :donecutoff [auto|1w|2w|3w|4w|all]";
    private const string SettingName = "board.done-cutoff";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary><c>view.done-cutoff</c>: the palette with the picker rows; refused on a tab that is not a Kanban board.</summary>
    public static ReduceResult Open(AppState state)
    {
        if (KanbanTab(state) is null)
        {
            return Refused(state, NoBoardText);
        }

        var opened = state with { Mode = InputMode.Command, PaletteText = Prefix, PaletteBack = 0, PaletteSelected = 0 };

        return new ReduceResult(opened with { Message = null }, _noEffects);
    }

    /// <summary>
    /// The picker rows for the palette (empty unless the active tab is a loaded Kanban board): follow Jira, then each
    /// of Jira's choices. The row of the value in use says so.
    /// </summary>
    public static IReadOnlyList<PaletteRow> Rows(AppState state)
    {
        if (KanbanTab(state) is not { } tab)
        {
            return [];
        }

        var rows = new List<PaletteRow>
        {
            Row(AutoWord, tab.DoneCutoffChoice is null ? $"follow Jira ({Describe(tab)})" : "follow Jira"),
        };

        foreach (var days in DoneCutoff.ChoiceDays.Append(DoneCutoff.AllDays))
        {
            var current = tab.DoneCutoffChoice == days ? " (current)" : string.Empty;

            rows.Add(Row(DoneCutoff.Word(days), $"{DoneCutoff.Label(days)}{current}"));
        }

        return rows;
    }

    /// <summary>
    /// <c>:donecutoff</c>: without an argument says what is applied and where it came from; <c>auto</c> follows
    /// Jira again, <c>1w</c> to <c>4w</c> and <c>all</c> choose for this tab. A change loads the board again.
    /// </summary>
    public static ReduceResult Command(AppState state, string argument)
    {
        if (KanbanTab(state) is not { } tab)
        {
            return Refused(state, NoBoardText);
        }

        if (argument.Length == 0)
        {
            return Refused(state, $"Done cutoff: {Describe(tab)}. {Usage}");
        }

        if (!DoneCutoff.TryParseChoice(argument, out var choice))
        {
            return Refused(state, Usage);
        }

        if (choice == tab.DoneCutoffChoice)
        {
            return Refused(state, $"Done cutoff: {Describe(tab)}.");
        }

        var effects = new List<Effect>();
        var reloaded = ViewReducer.Reopen(state, tab with { DoneCutoffChoice = choice }, effects);
        var text = choice is { } days
            ? $"Done cutoff for this tab: {DoneCutoff.Label(days)} (not saved in Jira)."
            : "Done cutoff follows Jira again.";

        return new ReduceResult(reloaded with { Message = text }, effects);
    }

    /// <summary>What is applied and where it came from, for example <c>2 weeks, from Jira</c>.</summary>
    public static string Describe(TabState tab)
    {
        var source = tab.CutoffSource switch
        {
            DoneCutoffSource.Jira => "from Jira",
            DoneCutoffSource.Choice => "your choice for this tab",
            _ => $"from the setting {SettingName}",
        };

        return $"{DoneCutoff.Label(tab.DoneCutoffDays)}, {source}";
    }

    private static PaletteRow Row(string word, string text)
    {
        return new PaletteRow(
            PaletteRowKind.Command,
            $"{PaletteCommands.DoneCutoff} {word}",
            $"{Prefix} {text}",
            string.Empty);
    }

    private static TabState? KanbanTab(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return tab is { CutoffSource: not null, Detail.Count: 0 } ? tab : null;
    }

    private static ReduceResult Refused(AppState state, string text)
    {
        return new ReduceResult(state with { Message = text }, _noEffects);
    }
}
