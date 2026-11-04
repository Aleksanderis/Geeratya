using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

/// <summary>
/// The keys of the issue detail that act on the cursor row or the shown issue: open the row, open it in the browser,
/// open the parent, jump between sections. <see cref="EnterBrowserRow"/> and <see cref="OpenInBrowser"/> return null
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

        if (target.Toggles)
        {
            return new ReduceResult(DetailReducer.ToggleExpanded(state, top.Tab, target.ExpandKey), _noEffects);
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

    /// <summary>
    /// <c>section.toggle</c> in the issue detail: folds the section under the cursor to a preview, or opens it again.
    /// Null when no issue is open, so the caller falls back to the list behaviour of the key.
    /// </summary>
    public static ReduceResult? ToggleSection(AppState state)
    {
        if (TopOf(state) is not { } top)
        {
            return null;
        }

        var section = top.Detail.Document is { } document ? document.SectionAt(top.Detail.Cursor) : 0;
        var next = DetailReducer.ToggleFolded(state, top.Tab, section);

        return next is null
            ? Say(state, "The fields cannot be folded.")
            : new ReduceResult(next, _noEffects);
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
    /// <c>detail.section-next</c> and <c>detail.section-previous</c>: the cursor goes to the start of the next
    /// section, or of the current one and then the one before it. At either end nothing moves.
    /// </summary>
    public static ReduceResult Section(AppState state, int direction)
    {
        if (TopOf(state) is not { } top || top.Detail.Document is not { } document)
        {
            return new ReduceResult(state, _noEffects);
        }

        int? line = null;

        foreach (var start in document.Sections)
        {
            if (direction > 0 && start > top.Detail.Cursor)
            {
                line = start;

                break;
            }

            if (direction < 0 && start < top.Detail.Cursor)
            {
                line = start;
            }
        }

        return new ReduceResult(line is { } target ? DetailReducer.JumpTo(state, top.Tab, target) : state, _noEffects);
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
