using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Backlog;

/// <summary>
/// The actions of the Scrum backlog sections: <c>section.toggle</c> (collapse or expand the section under the cursor)
/// and the section jumps <c>detail.section-next</c> and <c>detail.section-previous</c> when no issue is open. Each
/// returns null when the active tab is not a Scrum backlog (or an issue is open), so the caller goes on with what the
/// key does elsewhere.
/// </summary>
public static class BacklogActions
{
    private const string VisualText =
        "Leave Visual mode (Esc) before collapsing a section: hidden issues would be in the range.";

    private const string EmptyText = "The backlog has no issues yet.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary><c>section.toggle</c>.</summary>
    public static ReduceResult? Toggle(AppState state)
    {
        if (ActiveBacklog(state) is not { } tab)
        {
            return null;
        }

        if (tab.Rows.Count == 0)
        {
            return Say(state, EmptyText);
        }

        if (tab.VisualAnchor is not null)
        {
            return Say(state, VisualText);
        }

        return new ReduceResult(ViewReducer.Replace(state, BacklogCursor.Toggle(tab)), _noEffects);
    }

    /// <summary><c>detail.section-next</c> and <c>detail.section-previous</c> over the sections of the list.</summary>
    public static ReduceResult? Jump(AppState state, int direction)
    {
        if (ActiveBacklog(state) is not { } tab)
        {
            return null;
        }

        return new ReduceResult(ViewReducer.Replace(state, BacklogCursor.Jump(tab, direction)), _noEffects);
    }

    /// <summary>
    /// Enter on the header of a collapsed section expands it (the cursor stands on the header); null on any other
    /// row, which then opens as an issue.
    /// </summary>
    public static ReduceResult? Enter(AppState state)
    {
        if (ActiveBacklog(state) is not { } tab || !BacklogCursor.OnCollapsedHeader(tab))
        {
            return null;
        }

        return new ReduceResult(ViewReducer.Replace(state, BacklogCursor.Toggle(tab)), _noEffects);
    }

    private static TabState? ActiveBacklog(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return tab is { Detail.Count: 0 } && BacklogSections.Applies(tab) ? tab : null;
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }
}
