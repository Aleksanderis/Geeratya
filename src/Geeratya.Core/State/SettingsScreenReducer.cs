using Geeratya.Core.Keymap;
using Geeratya.Core.Settings;

namespace Geeratya.Core.State;

/// <summary>
/// The settings screen (<c>:settings</c>, <c>settings.open</c>): rows generated from the registry, edited in place.
/// It runs in Normal mode and reuses the Normal-mode keys: <c>j</c>/<c>k</c> move, <c>h</c>/<c>l</c> change the
/// category, <c>Enter</c> flips a toggle or opens the choice list of a choice, <c>Esc</c> closes the list, then the
/// screen. Every change goes through <see cref="SettingsReducer.Change"/>, so it applies at once and is saved by the
/// same effect <c>:set</c> uses. A theme in an open choice list is only previewed until <c>Enter</c>.
/// </summary>
public static class SettingsScreenReducer
{
    private const int PageRows = 5;

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>Actions that leave the screen for another overlay or end the run; they are not handled here.</summary>
    private static readonly HashSet<string> _passThrough =
    [
        ActionIds.Quit,
        ActionIds.HelpToggle,
        ActionIds.PaletteOpen,
        ActionIds.StatusHistory,
        ActionIds.StatusDismiss,
        ActionIds.ViewCycleCommandLevel,
    ];

    /// <summary>Opens the screen on the first row, or closes it when it is open (the key toggles).</summary>
    public static ReduceResult Toggle(AppState state)
    {
        if (state.SettingsScreen is not null)
        {
            return new ReduceResult(state with { SettingsScreen = null }, _noEffects);
        }

        var opened = state with
        {
            SettingsScreen = new SettingsScreenState(0),
            PendingKeys = string.Empty,
            Message = null,
        };

        return new ReduceResult(opened, _noEffects);
    }

    /// <summary>The highlighted row, clamped to the rows.</summary>
    public static int Selected(AppState state, int rowCount)
    {
        return Math.Clamp(state.SettingsScreen?.Selected ?? 0, 0, Math.Max(0, rowCount - 1));
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the open screen; null when the action is one that leaves the screen (the
    /// caller then handles it as usual), the unchanged state for every other action.
    /// </summary>
    public static ReduceResult? Handle(AppState state, string action)
    {
        if (state.SettingsScreen is null || _passThrough.Contains(action))
        {
            return null;
        }

        return action switch
        {
            ActionIds.NavigateDown => Move(state, 1),
            ActionIds.NavigateUp => Move(state, -1),
            ActionIds.NavigateTop => Jump(state, 0),
            ActionIds.NavigateBottom => Jump(state, int.MaxValue),
            ActionIds.NavigatePageDown => Move(state, PageRows),
            ActionIds.NavigatePageUp => Move(state, -PageRows),
            ActionIds.NavigateRight => StepCategory(state, 1),
            ActionIds.NavigateLeft => StepCategory(state, -1),
            ActionIds.ItemOpen => Activate(state),
            ActionIds.SettingsReset => Reset(state),
            ActionIds.SettingsOpen => Toggle(state),
            ActionIds.Cancel => Cancel(state),
            _ => new ReduceResult(state, _noEffects),
        };
    }

    /// <summary>A click on a category: the cursor goes to its first row.</summary>
    public static ReduceResult GoToCategory(AppState state, int category)
    {
        var rows = SettingsRows.Build(state);
        var names = SettingsRows.Categories(rows);

        if (state.SettingsScreen is null || category < 0 || category >= names.Count)
        {
            return new ReduceResult(state, _noEffects);
        }

        var first = rows.ToList().FindIndex(row => row.Category == names[category]);

        return Select(state, first);
    }

    /// <summary>
    /// A click or wheel step on a row: it is highlighted; a click then acts like Enter, a right-click resets.
    /// </summary>
    public static ReduceResult Clicked(AppState state, int index, string action)
    {
        if (state.SettingsScreen is null)
        {
            return new ReduceResult(state, _noEffects);
        }

        if (action is ActionIds.NavigateDown or ActionIds.NavigateUp)
        {
            return Move(state, action == ActionIds.NavigateDown ? 1 : -1);
        }

        var selected = Select(state, index);

        return action == ActionIds.SettingsReset ? Reset(selected.State) : Activate(selected.State);
    }

    /// <summary>A click on a word of the open choice list: it is taken like Enter on it.</summary>
    public static ReduceResult ChoiceClicked(AppState state, int choice)
    {
        if (state.SettingsScreen is not { Choice: not null } screen)
        {
            return new ReduceResult(state, _noEffects);
        }

        return Activate(state with { SettingsScreen = screen with { Choice = choice } });
    }

    private static ReduceResult Move(AppState state, int delta)
    {
        var rows = SettingsRows.Build(state);

        if (state.SettingsScreen is { Choice: { } choice } screen && CurrentSetting(state, rows) is { } setting)
        {
            var choices = SettingsRows.ChoicesOf(state, setting);

            return Previewed(state, screen with { Choice = Math.Clamp(choice + delta, 0, choices.Count - 1) }, setting);
        }

        return Select(state, Selected(state, rows.Count) + delta);
    }

    private static ReduceResult Jump(AppState state, int index)
    {
        if (state.SettingsScreen is { Choice: not null })
        {
            return new ReduceResult(state, _noEffects);
        }

        return Select(state, index);
    }

    private static ReduceResult Select(AppState state, int index)
    {
        var count = SettingsRows.Build(state).Count;
        var screen = state.SettingsScreen ?? new SettingsScreenState(0);
        var selected = Math.Clamp(index, 0, Math.Max(0, count - 1));

        // Another row ends an open choice list, as Esc would.
        var same = selected == screen.Selected;
        var moved = screen with
        {
            Selected = selected,
            Choice = same ? screen.Choice : null,
            Preview = same ? screen.Preview : null,
        };

        return new ReduceResult(state with { SettingsScreen = moved, Message = null }, _noEffects);
    }

    private static ReduceResult StepCategory(AppState state, int delta)
    {
        var rows = SettingsRows.Build(state);
        var names = SettingsRows.Categories(rows);
        var current = names.ToList().IndexOf(rows[Selected(state, rows.Count)].Category);
        var target = Math.Clamp(current + delta, 0, names.Count - 1);

        return GoToCategory(state, target);
    }

    private static ReduceResult Cancel(AppState state)
    {
        if (state.SettingsScreen is { Choice: not null } screen)
        {
            var closed = screen with { Choice = null, Preview = null };

            return new ReduceResult(state with { SettingsScreen = closed }, _noEffects);
        }

        return new ReduceResult(state with { SettingsScreen = null, Message = null }, _noEffects);
    }

    private static ReduceResult Activate(AppState state)
    {
        var rows = SettingsRows.Build(state);
        var row = rows[Selected(state, rows.Count)];

        switch (row.Kind)
        {
            case SettingsRowKind.Profiles:
                return ProfileReducer.OpenManager(state with { SettingsScreen = null });
            case SettingsRowKind.Info:
                return new ReduceResult(state with { Message = $"{row.Label}: {row.Value}" }, _noEffects);
        }

        var setting = row.Setting!;
        var choices = SettingsRows.ChoicesOf(state, setting);
        var screen = state.SettingsScreen!;

        if (screen.Choice is { } picked)
        {
            var accepted = state with { SettingsScreen = screen with { Choice = null, Preview = null } };

            return SettingsReducer.Change(accepted, setting, choices[Math.Clamp(picked, 0, choices.Count - 1)]);
        }

        if (choices.Count == 0)
        {
            var message = $"Change it with :set {setting.Name} <value>.";

            return new ReduceResult(state with { Message = message }, _noEffects);
        }

        var current = setting.Read(state.Settings);

        if (setting.Kind == SettingKind.Toggle)
        {
            var flipped = current == SettingsRegistry.On ? SettingsRegistry.Off : SettingsRegistry.On;

            return SettingsReducer.Change(state, setting, flipped);
        }

        var listed = screen with { Choice = Math.Max(0, choices.ToList().IndexOf(current)) };

        return new ReduceResult(state with { SettingsScreen = listed }, _noEffects);
    }

    private static ReduceResult Reset(AppState state)
    {
        var rows = SettingsRows.Build(state);
        var row = rows[Selected(state, rows.Count)];
        var closed = state with { SettingsScreen = state.SettingsScreen! with { Choice = null, Preview = null } };

        if (row.Setting is not { } setting)
        {
            return new ReduceResult(closed with { Message = "Nothing to reset on this row." }, _noEffects);
        }

        if (setting.Read(closed.Settings) == setting.Default)
        {
            var message = $"{setting.Name} is already the default ({setting.Default}).";

            return new ReduceResult(closed with { Message = message }, _noEffects);
        }

        return SettingsReducer.Change(closed, setting, setting.Default);
    }

    // Moving through the theme list draws each theme without saving; Esc or leaving restores the saved one.
    private static ReduceResult Previewed(AppState state, SettingsScreenState screen, SettingDefinition setting)
    {
        if (setting.Name != SettingsRegistry.ThemeName)
        {
            return new ReduceResult(state with { SettingsScreen = screen }, _noEffects);
        }

        var name = SettingsRows.ChoicesOf(state, setting)[screen.Choice!.Value];

        return new ReduceResult(
            state with { SettingsScreen = screen with { Preview = name } },
            [new ApplyThemeEffect(name, Preview: true)]);
    }

    private static SettingDefinition? CurrentSetting(AppState state, IReadOnlyList<SettingsRow> rows)
    {
        return rows[Selected(state, rows.Count)].Setting;
    }

    /// <summary>
    /// After any command: a theme that was only previewed and is no longer in an open list (Esc, another row, the
    /// screen left) is replaced by the saved theme, unless the same step applied one for real.
    /// </summary>
    public static ReduceResult RestorePreview(AppState before, ReduceResult result)
    {
        if (before.SettingsScreen?.Preview is null || result.State.SettingsScreen?.Preview is not null
            || result.Effects.OfType<ApplyThemeEffect>().Any())
        {
            return result;
        }

        var restore = new ApplyThemeEffect(result.State.Settings.Theme, Preview: true);

        return new ReduceResult(result.State, [.. result.Effects, restore]);
    }
}
