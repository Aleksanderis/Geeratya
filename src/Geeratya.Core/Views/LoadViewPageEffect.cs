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
/// issues that reached Done longer ago than this; 0 shows every Done issue. It is the last fallback.</param>
/// <param name="DoneCutoffChoice">The tab's quick choice (<c>view.done-cutoff</c>): days, 0 for all, null for none.
/// It wins over what Jira says and over the setting. Stamped like <paramref name="DoneCutoffDays"/>.</param>
/// <param name="PinnedCutoff">Set on the later pages of a Kanban board load: the days and source the first page
/// applied, so every page of one load is cut by the same value even if Jira's answer expires or is retried in
/// between (#699). Null on the first page; stamped by <c>AppReducer</c> from the tab.</param>
public sealed record LoadViewPageEffect(
    TabId Tab,
    ViewDefinition Definition,
    PageCursor? Cursor,
    long RequestId,
    LoadReason Reason,
    bool Background,
    string? ExtraJql = null,
    int DoneCutoffDays = DoneCutoff.DefaultDays,
    int? DoneCutoffChoice = null,
    PinnedCutoff? PinnedCutoff = null) : Effect;

/// <summary>The Done cutoff the first page of a board load applied, carried to its later pages.</summary>
/// <param name="Days">The days; 0 for none.</param>
/// <param name="Source">Where the days came from.</param>
public sealed record PinnedCutoff(int Days, DoneCutoffSource Source);
