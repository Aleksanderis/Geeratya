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

        if (target.Toggles && target.Kind != DetailTargetKind.Field)
        {
            return new ReduceResult(DetailReducer.ToggleExpanded(state, top.Tab, target), _noEffects);
        }

        if (target is { Kind: DetailTargetKind.Field, Value: DetailFieldIds.Attachments })
        {
            return new ReduceResult(DetailReducer.ZoomPane(state, top.Tab, PaneIds.Attachments), _noEffects);
        }

        if (target.Kind == DetailTargetKind.Field)
        {
            return DetailFieldActions.Open(state, target.Value);
        }

        return target.Kind is DetailTargetKind.Issue or DetailTargetKind.Comment ? null : OpenTarget(state, target);
    }

    /// <summary>
    /// <c>section.toggle</c> (<c>z a</c>) in an issue detail: expands the cut Description or comment under the cursor,
    /// or cuts it again. Null outside an issue detail; the key does nothing on a row without such a text.
    /// </summary>
    public static ReduceResult? ToggleText(AppState state)
    {
        if (TopOf(state) is not { } top)
        {
            return null;
        }

        return DetailReducer.TargetOf(top.Detail) is { Toggles: true } target
            ? new ReduceResult(DetailReducer.ToggleExpanded(state, top.Tab, target), _noEffects)
            : new ReduceResult(state, _noEffects);
    }

    /// <summary>
    /// <c>detail.edit</c> (<c>i</c>): the title or Description pane under the cursor goes into its edit state, like
    /// <c>Enter</c> on it. Outside an issue detail the key does nothing.
    /// </summary>
    public static ReduceResult EditText(AppState state)
    {
        if (TopOf(state) is not { } top)
        {
            return new ReduceResult(state, _noEffects);
        }

        var target = DetailReducer.TargetOf(top.Detail);

        if (target is { Kind: DetailTargetKind.Field, Value: DetailFieldIds.Summary or DetailFieldIds.Description })
        {
            return DetailFieldActions.Open(state, target.Value);
        }

        return Say(state, "i edits the title or the Description: move to one of them (Enter edits any field).");
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
    /// <c>pane.zoom</c> zooms the pane that holds the cursor (or leaves the zoom); while zoomed, <c>pane.focus-left</c>
    /// and <c>pane.focus-right</c> switch the section of the strip and the other two do nothing.
    /// </summary>
    public static ReduceResult? FocusPane(AppState state, string? action)
    {
        if (!PaneActions.IsPaneAction(action) || TopOf(state) is not { } top)
        {
            return null;
        }

        if (action == ActionIds.PaneZoom)
        {
            return new ReduceResult(DetailReducer.ToggleZoom(state, top.Tab), _noEffects);
        }

        if (top.Detail.Zoomed is not null)
        {
            return new ReduceResult(ZoomedFocus(state, top.Tab, action), _noEffects);
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
        var onSide = top.Detail.SideCursor is not null && document.Side is not null;
        var current = onSide ? PaneIds.Details : document.SectionIds[document.SectionAt(top.Detail.Cursor)];
        var panes = PaneReducer.Focus(new PaneState(grid, current), direction);
        var chosen = panes.Current ?? current;

        if (document.Side is not null && (chosen == PaneIds.Details) != onSide)
        {
            return new ReduceResult(DetailReducer.FocusSide(state, top.Tab, chosen == PaneIds.Details), _noEffects);
        }

        var target = document.SectionIds.ToList().IndexOf(chosen);

        if (target < 0 || document.SectionIds[target] == current)
        {
            return new ReduceResult(state, _noEffects);
        }

        return new ReduceResult(DetailReducer.JumpToPane(state, top.Tab, target), _noEffects);
    }

    private static AppState ZoomedFocus(AppState state, TabState tab, string? action)
    {
        return action switch
        {
            ActionIds.PaneFocusLeft => DetailReducer.SwitchSection(state, tab, -1),
            ActionIds.PaneFocusRight => DetailReducer.SwitchSection(state, tab, 1),
            _ => state,
        };
    }

    /// <summary>
    /// The panes of a document as a grid: one column with a row per pane, in document order, and the Details sidebar
    /// as a second column when the document has one.
    /// </summary>
    public static PaneGrid Grid(DetailDocument document)
    {
        var slots = document.SectionIds.Select(static (id, row) => new PaneSlot(id, 0, row)).ToList();

        if (document.Side is not null)
        {
            slots.Add(new PaneSlot(PaneIds.Details, 1, 0, 1, Math.Max(1, slots.Count)));
        }

        return new PaneGrid(slots);
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
