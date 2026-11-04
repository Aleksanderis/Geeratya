using Geeratya.Core.Keymap;
using Geeratya.Core.Panes;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

/// <summary>
/// The keys of the issue detail that act on the cursor row or the shown issue: open the row, open it in the browser,
/// open the parent, move between panes. <see cref="EnterBrowserRow"/> and <see cref="OpenInBrowser"/> return null
/// when no issue is open on the active tab, so the caller falls back to the list behaviour of the same key.
/// </summary>
internal static class DetailActions
{
    private const string StackFullText = "Too many issues open: press Esc to go back.";
    private const string ZoomPendingText = "Zoom is not available in the issue detail yet.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>
    /// Enter on a remote link or attachment row opens it in the browser; other rows are left to the caller.
    /// </summary>
    public static ReduceResult? EnterBrowserRow(AppState state)
    {
        if (TopOf(state) is not { } top || DetailReducer.TargetOf(top.Detail) is not { } target)
        {
            return null;
        }

        if (target.Toggles)
        {
            return new ReduceResult(DetailReducer.ToggleExpanded(state, top.Tab, target), _noEffects);
        }

        if (target.Kind == DetailTargetKind.Field)
        {
            return DetailFieldActions.Open(state, target.Value);
        }

        return target.Kind is DetailTargetKind.Issue or DetailTargetKind.Comment ? null : OpenTarget(state, target);
    }

    /// <summary>
    /// <c>issue.open-in-browser</c>: the cursor row when it is a remote link, attachment or related issue, else the
    /// shown issue.
    /// </summary>
    public static ReduceResult? OpenInBrowser(AppState state)
    {
        if (TopOf(state) is not { } top)
        {
            return null;
        }

        var target = DetailReducer.TargetOf(top.Detail);

        if (target is null or { Kind: DetailTargetKind.Comment or DetailTargetKind.Field })
        {
            return BrowserReducer.OpenIssue(state, top.Detail.Key);
        }

        return OpenTarget(state, target);
    }

    /// <summary><c>issue.open-parent</c>: pushes the parent of the shown issue.</summary>
    public static ReduceResult OpenParent(AppState state)
    {
        if (TopOf(state) is not { } top)
        {
            return Say(state, "Open an issue first.");
        }

        var parent = top.Detail.Issue is { } issue ? issue.Parent?.Key : top.Detail.Row?.Parent?.Key;

        if (parent is null)
        {
            return Say(state, $"{top.Detail.Key} has no parent.");
        }

        var effects = new List<Effect>();
        var next = DetailReducer.Open(state, top.Tab.Id, parent, null, effects);

        return next == state ? Say(state, StackFullText) : new ReduceResult(next, effects);
    }

    /// <summary>
    /// <c>pane.focus-*</c> in the issue detail: the cursor goes to the first row of the neighbouring pane, and the
    /// pane is shown from its top edge. Null when no issue is open, so the caller falls back to the other pane users.
    /// <c>pane.zoom</c> says that zoom is not built yet (the zoomed sections are #705).
    /// </summary>
    public static ReduceResult? FocusPane(AppState state, string? action)
    {
        if (!PaneActions.IsPaneAction(action) || TopOf(state) is not { } top)
        {
            return null;
        }

        if (action == ActionIds.PaneZoom)
        {
            return Say(state, ZoomPendingText);
        }

        if (top.Detail.Document is not { } document || document.SectionIds.Count == 0)
        {
            return new ReduceResult(state, _noEffects);
        }

        var direction = action switch
        {
            ActionIds.PaneFocusLeft => PaneDirection.Left,
            ActionIds.PaneFocusDown => PaneDirection.Down,
            ActionIds.PaneFocusUp => PaneDirection.Up,
            _ => PaneDirection.Right,
        };
        var grid = Grid(document);
        var current = document.SectionIds[document.SectionAt(top.Detail.Cursor)];
        var panes = PaneReducer.Focus(new PaneState(grid, current), direction);
        var target = document.SectionIds.ToList().IndexOf(panes.Current ?? current);

        if (target < 0 || document.SectionIds[target] == current)
        {
            return new ReduceResult(state, _noEffects);
        }

        return new ReduceResult(DetailReducer.JumpToPane(state, top.Tab, target), _noEffects);
    }

    /// <summary>The panes of a document as a grid: one column, one row per pane, in document order.</summary>
    public static PaneGrid Grid(DetailDocument document)
    {
        return new PaneGrid([.. document.SectionIds.Select(static (id, row) => new PaneSlot(id, 0, row))]);
    }

    private static ReduceResult OpenTarget(AppState state, DetailTarget target)
    {
        return target.Kind switch
        {
            DetailTargetKind.RemoteLink => BrowserReducer.Request(state, new OpenUrlRequested(target.Value, true)),
            _ => BrowserReducer.OpenIssue(state, target.Value),
        };
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
