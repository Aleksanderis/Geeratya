using Geeratya.Core.Boards;
using Geeratya.Core.CommandPalette;
using Geeratya.Core.Detail;
using Geeratya.Core.Help;
using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Lists;
using Geeratya.Core.Selection;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;

namespace Geeratya.Core.State;

public sealed partial class AppReducer
{
    // Esc in the body: a picked-up rank issue goes first, then Visual mode, then the marks.
    private static AppState Escaped(AppState state)
    {
        return DetailYank.Back(state) ?? SelectionActions.Back(state) ?? RankActions.DropPickup(state);
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
            HelpFilterBack = 0,
            HelpFiltering = false,
            MessagesOpen = false,
            MessagesShowSettings = false,
            Confirm = null,
            Form = null,
            Picker = null,
            Jump = null,
            FlashJump = null,
            Editor = null,
            ProfileList = toManager ? state.ProfileList : null,
            SettingsScreen = null,
            ContextMenu = null,
        };
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

        return PromptEditing.Backspace(state);
    }

    private AppState MovePalette(AppState state, int delta)
    {
        var count = PaletteRows.Selectable(state, _keymap).Count;

        return state with { PaletteSelected = Math.Clamp(state.PaletteSelected + delta, 0, Math.Max(0, count - 1)) };
    }
}
