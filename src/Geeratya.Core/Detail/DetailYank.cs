using Geeratya.Core.Clipboard;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

/// <summary>
/// Select and copy text in the issue detail (#773): <c>v</c> starts a line selection at the cursor, the motions extend
/// it, <c>y</c> copies it and <c>Esc</c> drops it; without a selection <c>y</c> copies the whole field or comment under
/// the cursor. A drag with the mouse selects and copies on release. The copy itself is an effect. Pure.
/// </summary>
internal static class DetailYank
{
    private const string SideText = "Select text in the main column; y copies the field under the cursor";
    private const string NoIssueText = "Open an issue to copy from it";
    private const string NothingText = "Nothing to copy here";
    private const string StartedText = "Visual: move to extend, y copies, Esc cancels";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>
    /// <c>v</c> in an issue detail: starts the selection on the cursor line, or ends it. A cut Description or comment
    /// is shown in full first, so the selection can cover all of it. Null outside an issue detail.
    /// </summary>
    public static ReduceResult? ToggleVisual(AppState state)
    {
        if (TopOf(state) is not { } opened)
        {
            return null;
        }

        var detail = opened.Detail;

        if (detail.YankAnchor is not null)
        {
            return Replaced(state, opened.Tab, detail with { YankAnchor = null });
        }

        if (detail.SideCursor is not null && detail.Document?.Side is not null)
        {
            return Say(state, SideText);
        }

        var current = opened;

        if (DetailReducer.TargetOf(detail) is { Expands: true, Note: false } cut)
        {
            var expanded = DetailReducer.ToggleExpanded(state, opened.Tab, cut);

            current = TopOf(expanded) ?? opened;
            state = expanded;
        }

        if (DetailText.AnchorAt(current.Detail, current.Detail.Cursor) is not { } anchor)
        {
            return Say(state, NothingText);
        }

        var started = Replaced(state, current.Tab, current.Detail with { YankAnchor = anchor });

        return Say(started.State, StartedText);
    }

    /// <summary>
    /// <c>y</c>: copies the selection and ends it, else the whole field or comment under the cursor. Null outside an
    /// issue detail.
    /// </summary>
    public static ReduceResult? Yank(AppState state)
    {
        if (TopOf(state) is not { } opened)
        {
            return null;
        }

        var detail = opened.Detail;

        if (detail.Document is null)
        {
            return Say(state, NoIssueText);
        }

        var text = detail.YankAnchor is { } anchor ? DetailText.Selected(detail, anchor) : DetailText.Field(detail);
        var cleared = detail with { YankAnchor = null };
        var ended = detail.YankAnchor is null ? state : Replaced(state, opened.Tab, cleared).State;

        if (text.Length == 0)
        {
            return Say(ended, NothingText);
        }

        var description = Describe(text, detail.YankAnchor is not null);

        return new ReduceResult(ended, [new CopyToClipboardEffect(text, description)]);
    }

    /// <summary>
    /// Esc: drops the selection. Null when there is none, so the key goes on to do what it does otherwise.
    /// </summary>
    public static AppState? Back(AppState state)
    {
        if (state.Mode != Keymap.InputMode.Normal || TopOf(state) is not { Detail.YankAnchor: not null } opened)
        {
            return null;
        }

        return Replaced(state, opened.Tab, opened.Detail with { YankAnchor = null }).State;
    }

    /// <summary>True when Esc has a selection to drop in the issue detail.</summary>
    public static bool CanBack(AppState state)
    {
        return state.Mode == Keymap.InputMode.Normal && TopOf(state) is { Detail.YankAnchor: not null };
    }

    /// <summary>
    /// A drag over the main column of the issue detail: the line where the button went down starts the selection, the
    /// line where it came up ends it, and the text is copied.
    /// </summary>
    public static ReduceResult Dragged(AppState state, DetailDragged drag)
    {
        var placed = ViewReducer.PlaceCursor(state, new PointerPlaced(drag.FromColumn, drag.FromRow, drag.Width));

        if (state.Mode != Keymap.InputMode.Normal || placed is null || TopOf(placed.State) is not { } start
            || start.Detail.SideCursor is not null)
        {
            return new ReduceResult(state, _noEffects);
        }

        if (DetailText.AnchorAt(start.Detail, start.Detail.Cursor) is not { } anchor)
        {
            return new ReduceResult(placed.State, placed.Effects);
        }

        var anchored = Replaced(placed.State, start.Tab, start.Detail with { YankAnchor = anchor }).State;
        var ended = ViewReducer.PlaceCursor(anchored, new PointerPlaced(drag.ToColumn, drag.ToRow, drag.Width));

        if (ended is null || TopOf(ended.State) is { Detail.SideCursor: not null })
        {
            return new ReduceResult(anchored, placed.Effects);
        }

        var yanked = Yank(ended.State) ?? ended;

        return new ReduceResult(yanked.State, [.. placed.Effects, .. ended.Effects, .. yanked.Effects]);
    }

    private static string Describe(string text, bool selection)
    {
        var lines = text.Count(static character => character == '\n') + 1;

        if (!selection)
        {
            return lines == 1 ? "the text under the cursor" : $"{lines} lines";
        }

        return lines == 1 ? "1 line" : $"{lines} lines";
    }

    private static ReduceResult Replaced(AppState state, TabState tab, DetailState detail)
    {
        var stack = tab.Detail.ToArray();

        stack[^1] = detail;

        return new ReduceResult(ViewReducer.Replace(state, tab with { Detail = stack }), _noEffects);
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }

    private static Opened? TopOf(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return tab is { Detail.Count: > 0 } ? new Opened(tab, tab.Detail[^1]) : null;
    }

    private sealed record Opened(TabState Tab, DetailState Detail);
}
