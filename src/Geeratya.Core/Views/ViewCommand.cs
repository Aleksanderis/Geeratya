using Geeratya.Core.State;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Views;

/// <summary>A command handled by <see cref="ViewReducer"/>.</summary>
public abstract record ViewCommand : Command;

/// <summary>Open a tab for a view and activate it.</summary>
public sealed record ViewOpenRequested(ViewDefinition Definition) : ViewCommand;

/// <summary>Make a tab the active one; a cold tab reloads.</summary>
public sealed record TabActivated(TabId Tab) : ViewCommand;

/// <summary>Close a tab and cancel its work.</summary>
public sealed record TabClosed(TabId Tab) : ViewCommand;

/// <summary>
/// Show another sprint in a sprint tab: the tab's source value becomes <paramref name="Sprint"/> and its first page is
/// loaded again; the old load is cancelled and its late reply ignored. Does nothing for other tabs or the same sprint.
/// </summary>
public sealed record SprintSwitched(TabId Tab, SprintSource Sprint, string Title) : ViewCommand;

/// <summary>Change how a tab shows its rows; everything else is kept.</summary>
public sealed record ViewLayoutChanged(TabId Tab, ViewLayout Layout) : ViewCommand;

/// <summary>
/// The viewport changed; applies to every tab. <paramref name="Rows"/> is the list's row count,
/// <paramref name="Columns"/> the terminal width (0 when the sender does not know it: the last width is kept),
/// <paramref name="BodyHeight"/> the rows the frame gives the body (negative when unknown: the last one is kept).
/// </summary>
public sealed record ViewportChanged(int Rows, int Columns = 0, int BodyHeight = -1) : ViewCommand;

/// <summary>Load the next page of a tab.</summary>
public sealed record NextPageRequested(TabId Tab) : ViewCommand;

/// <summary>
/// The selection of a tab moved to <paramref name="Index"/> (clamped to the loaded rows); within one screen of the
/// end it asks for the next page. The list motion (#79) sends it.
/// </summary>
public sealed record SelectionMoved(TabId Tab, int Index) : ViewCommand;

/// <summary>A page arrived; applied only while <paramref name="RequestId"/> is the tab's current request.</summary>
public sealed record ViewPageLoaded(TabId Tab, long RequestId, IssuePage Page, DateTimeOffset At) : ViewCommand;

/// <summary>A load failed; applied only while <paramref name="RequestId"/> is the tab's current request.</summary>
public sealed record ViewLoadFailed(TabId Tab, long RequestId, JiraError Error, DateTimeOffset At) : ViewCommand;

/// <summary>
/// The board configuration arrived, ahead of the first issue page of a board or backlog load; applied only while
/// <paramref name="RequestId"/> is the tab's current request.
/// </summary>
public sealed record BoardConfigurationLoaded(TabId Tab, long RequestId, BoardConfiguration Configuration)
    : ViewCommand;

/// <summary>
/// The quick filters of the tab's board arrived (<see cref="LoadQuickFiltersEffect"/>). <paramref name="Apply"/> says
/// a filter key started the lookup, so the key's action goes on with them; <paramref name="Failure"/> is the text for
/// the status line when the lookup failed.
/// </summary>
public sealed record QuickFiltersLoaded(
    TabId Tab,
    IReadOnlyList<QuickFilter> Filters,
    bool Apply,
    string? Failure = null) : ViewCommand;

/// <summary>
/// The sprint a Sprint view shows arrived (null: the board has no active sprint; <paramref name="LookupFailed"/> when the lookup failed instead), after the
/// board configuration; applied only while <paramref name="RequestId"/> is the tab's current request.
/// </summary>
public sealed record SprintLoaded(TabId Tab, long RequestId, Sprint? Sprint, bool LookupFailed = false)
    : ViewCommand;

/// <summary>
/// The open and future sprints of a Scrum backlog, reported ahead of its first issue page; applied only while
/// <paramref name="RequestId"/> is the tab's current request.
/// </summary>
public sealed record SprintsLoaded(TabId Tab, long RequestId, IReadOnlyList<Sprint> Sprints) : ViewCommand;

/// <summary>
/// The sprint probe found that a plain backlog (a team-managed board, typed <c>simple</c>) is a Scrum board: the tab
/// becomes the Scrum backlog (sprint sections). Applied only while <paramref name="RequestId"/> is the tab's current request.
/// </summary>
public sealed record BacklogBecameScrum(TabId Tab, long RequestId) : ViewCommand;

/// <summary>
/// The host's clock tick (once a second while a tab is active). The reducer decides from it whether the active tab
/// refreshes; time is only ever read from here and from the replies.
/// </summary>
public sealed record Tick(DateTimeOffset Now) : ViewCommand;

/// <summary>Refresh a tab now on the user lane (the refresh key); a failed next page is retried instead.</summary>
public sealed record RefreshRequested(TabId Tab) : ViewCommand;

/// <summary>
/// The transport reports its rate-limit state: <see cref="RateLimited"/> while a wait is active, <see cref="Online"/>
/// when it ended (which clears only a rate-limit state, not an offline or rejected link).
/// </summary>
public sealed record LinkChanged(SiteLink Link) : ViewCommand;

/// <summary>
/// The approximate number of matches of a JQL source, reported after its first page. Applied to the tab if it still
/// exists; a failed count call posts nothing and the header keeps <c>N+</c>.
/// </summary>
public sealed record ViewTotalLoaded(TabId Tab, int Total) : ViewCommand;
