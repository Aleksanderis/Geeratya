using Geeratya.Core.Boards;
using Geeratya.Core.State;
using Geeratya.Jira.Paging;

namespace Geeratya.Core.Views;

/// <summary>
/// Load one page of a view. Data only; the host answers with <see cref="ViewPageLoaded"/> or
/// <see cref="ViewLoadFailed"/>.
/// </summary>
/// <param name="Tab">The tab the page is for.</param>
/// <param name="Definition">The view to load.</param>
/// <param name="Cursor">The page to load, null for the first.</param>
/// <param name="RequestId">The id the reply must carry.</param>
/// <param name="Reason">Why the page is loaded.</param>
/// <param name="Background">Run in the background lane (refreshes) rather than the user lane.</param>
/// <param name="ExtraJql">The JQL of the switched-on quick filters, null when none (#192). Not part of the cache
/// key, so a load with it neither reads nor writes the view cache.</param>
/// <param name="DoneCutoffDays">The setting <c>board.done-cutoff</c> when the load was issued: a Kanban board hides
/// issues resolved longer ago than this; 0 shows every Done issue.</param>
public sealed record LoadViewPageEffect(
    TabId Tab,
    ViewDefinition Definition,
    PageCursor? Cursor,
    long RequestId,
    LoadReason Reason,
    bool Background,
    string? ExtraJql = null,
    int DoneCutoffDays = DoneCutoff.DefaultDays) : Effect;
