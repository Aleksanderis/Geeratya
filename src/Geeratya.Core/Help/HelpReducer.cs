using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Core.Help;

/// <summary>
/// The state changes of the help overlay's pages and filter (<c>docs/ux.md</c>, "Help"): switching pages, typing the
/// filter and the two-step <c>Esc</c> (the first clears the filter, the next closes help). Pure.
/// </summary>
public static class HelpReducer
{
    private const string EscapeKey = "Esc";
    private const string EnterKey = "Enter";
    private const string BackspaceKey = "Backspace";
    private const string NextPageKey = "Tab";
    private const string PreviousPageKey = "Shift-Tab";

    /// <summary>
    /// True while the filter line is typed and takes the printable keys (the Messages overlay has no filter).
    /// </summary>
    public static bool TakesKeys(AppState state)
    {
        return state is { Mode: InputMode.Help, HelpFiltering: true, MessagesOpen: false };
    }

    /// <summary>Starts typing the filter; the text typed so far stays and is edited.</summary>
    public static AppState OpenFilter(AppState state)
    {
        return IsPages(state) ? state with { HelpFiltering = true } : state;
    }

    /// <summary>Shows the next (<paramref name="direction"/> 1) or previous (-1) page.</summary>
    public static AppState Step(AppState state, KeymapTable keymap, int direction)
    {
        return IsPages(state) ? Show(state, HelpPages.Step(state, keymap, direction)) : state;
    }

    /// <summary>Shows the page at <paramref name="index"/> (a click on its name).</summary>
    public static AppState Show(AppState state, int index)
    {
        return IsPages(state) ? state with { HelpPage = index, HelpScroll = 0 } : state;
    }

    /// <summary>
    /// <c>help.close</c>: with a filter set the first press only clears it, so a second press closes help; returns
    /// null when help should close.
    /// </summary>
    public static AppState? Close(AppState state)
    {
        return IsPages(state) && state.HelpFilter.Length > 0 ? Cleared(state) : null;
    }

    /// <summary>A key while the filter line is typed: text, Backspace, Enter (keep), Esc (clear), Tab (page).</summary>
    public static AppState Key(AppState state, string key, KeymapTable keymap)
    {
        switch (key)
        {
            case EscapeKey:
                return Cleared(state);
            case EnterKey:
                return state with { HelpFiltering = false };
            case BackspaceKey:
                return Erased(state);
            case NextPageKey:
                return Step(state, keymap, 1);
            case PreviousPageKey:
                return Step(state, keymap, -1);
        }

        return KeyNotation.ToText(key) is { } text ? Typed(state, $"{state.HelpFilter}{text}") : state;
    }

    private static bool IsPages(AppState state)
    {
        return state is { Mode: InputMode.Help, MessagesOpen: false };
    }

    private static AppState Cleared(AppState state)
    {
        return state with { HelpFilter = string.Empty, HelpFiltering = false, HelpScroll = 0 };
    }

    private static AppState Erased(AppState state)
    {
        if (state.HelpFilter.Length == 0)
        {
            return state with { HelpFiltering = false };
        }

        return Typed(state, TextElements.RemoveLast(state.HelpFilter));
    }

    // The filter changed, so the page starts at its first line again.
    private static AppState Typed(AppState state, string filter)
    {
        return state with { HelpFilter = filter, HelpScroll = 0 };
    }
}
