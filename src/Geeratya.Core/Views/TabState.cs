using Geeratya.Core.Boards;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Views;

/// <summary>
/// One tab: a view definition plus everything shown and loaded for it. Selection, scroll and layout stay while the
/// tab is hidden. The rows are an immutable list of the Jira layer's own models; a page append builds a new list.
/// </summary>
/// <param name="Id">The tab id.</param>
/// <param name="Definition">The source and layout (the layout lives in the definition).</param>
/// <param name="Rows">The loaded rows, at most <see cref="ViewPaging.MaxRowsPerTab"/>.</param>
/// <param name="Next">The cursor of the next page, null when the last page is loaded.</param>
/// <param name="Truncated">The rows hit the cap; further pages are not loaded.</param>
/// <param name="Selected">The selected row index.</param>
/// <param name="SelectedKey">The issue key the selection points at; survives a reload that moves rows.</param>
/// <param name="Scroll">The first visible row.</param>
/// <param name="VisibleRows">The last viewport height the host reported.</param>
/// <param name="Load">What the tab is doing.</param>
/// <param name="RequestId">The only request whose reply this tab accepts; 0 while no request is in flight.</param>
/// <param name="LastActiveSeq">The sequence value when the tab was last active; picks the tab to go cold.</param>
/// <param name="IsStale">The rows are old (cache, or a refresh failed).</param>
/// <param name="LoadedAt">When the rows last came from the network.</param>
/// <param name="PendingRows">Rows gathered by a multi-page refresh, swapped in with the last page.</param>
/// <param name="Cold">The tab gave its rows up; activation reloads it.</param>
/// <param name="Board">The board configuration of a board or backlog tab; null for other sources.</param>
/// <param name="Failures">Failed loads in a row that count towards the backoff (a failed next page does not).</param>
/// <param name="NextRefreshAt">When the next automatic refresh is due, null while unknown (never loaded).</param>
/// <param name="KeyWidth">The widest issue key among the loaded rows, kept so the list needs no scan per frame.</param>
/// <param name="Cursor">The selected column and the selected and first visible card of each column in the columns
/// layout; null until the layout is first used, and dropped when the tab goes cold or changes layout.</param>
public sealed record TabState(
    TabId Id,
    ViewDefinition Definition,
    IReadOnlyList<IssueSummary> Rows,
    PageCursor? Next,
    bool Truncated,
    int Selected,
    string? SelectedKey,
    int Scroll,
    int VisibleRows,
    LoadState Load,
    long RequestId,
    long LastActiveSeq,
    bool IsStale,
    DateTimeOffset? LoadedAt,
    IReadOnlyList<IssueSummary>? PendingRows,
    bool Cold,
    int KeyWidth,
    BoardConfiguration? Board = null,
    int Failures = 0,
    DateTimeOffset? NextRefreshAt = null,
    BoardCursor? Cursor = null)
{
    /// <summary>
    /// The issue stack of the tab, last is the one shown; empty while the view itself is shown. Closing the tab or
    /// switching the profile drops it.
    /// </summary>
    public IReadOnlyList<DetailState> Detail { get; init; } = [];

    /// <summary>
    /// The refresh key was pressed while a write ran: the refresh starts when the last operation ends
    /// (<c>docs/architecture.md</c>, "Write pipeline").
    /// </summary>
    public bool RefreshQueued { get; init; }

    /// <summary>
    /// Issues whose write ended while this tab's load was in flight: the load may have started before the write, so
    /// its reply keeps the tab's own rows for these keys. Cleared when the load ends.
    /// </summary>
    public IReadOnlyList<string> ProtectedKeys { get; init; } = [];

    /// <summary>The open field form (edit, later create and transition) of this tab; null when none.</summary>
    public FormState? Form { get; init; }

    /// <summary>
    /// The rows Jira returned for <see cref="ProtectedKeys"/> when the write ended. A reply keeps these for a key the
    /// tab does not hold yet (a next page that arrives after the write). Cleared with the keys.
    /// </summary>
    public IReadOnlyList<IssueSummary> ProtectedRows { get; init; } = [];

    /// <summary>
    /// The issue picked up by the rank-to key and waiting to be dropped at the issue under the cursor; null when
    /// none. Scoped to the tab, like the selection.
    /// </summary>
    public string? RankPickup { get; init; }

    /// <summary>
    /// The issue keys marked with <c>selection.mark</c>, scattered rows allowed. Keys, not indexes, so the marks
    /// survive scrolling and a refresh that moves rows (<c>docs/ux-keymap.md</c>, "Selection and counts").
    /// </summary>
    public IReadOnlyList<string> Marks { get; init; } = [];

    /// <summary>
    /// The issue key where Visual mode started; null outside Visual mode. The range runs from this row to the
    /// selected row.
    /// </summary>
    public string? VisualAnchor { get; init; }

    /// <summary>
    /// Issues deleted while this tab's load was in flight: the reply may still hold them (it started before the
    /// delete, or the search index lags), so it drops these keys. Cleared when the load ends.
    /// </summary>
    public IReadOnlyList<string> RemovedKeys { get; init; } = [];

    /// <summary>
    /// The number of issues the source has, when known: from the paged reply of a board or backlog, or from the
    /// approximate-count call of a JQL source. Null while unknown (the header then reads <c>N+</c>).
    /// </summary>
    public int? Total { get; init; }

    /// <summary>The <see cref="Total"/> is an approximation (JQL sources), so the header says "about".</summary>
    public bool TotalIsApproximate { get; init; }

    /// <summary>The sprint a Sprint view shows (name, goal, dates); null while unknown or when there is none.</summary>
    public Sprint? Sprint { get; init; }

    /// <summary>
    /// The sprint lookup finished, so a null <see cref="Sprint"/> means the board has no sprint to show rather than
    /// "not loaded yet".
    /// </summary>
    public bool SprintResolved { get; init; }

    /// <summary>
    /// The days after which the board load left out resolved issues (a Kanban board, like Jira's "Hide done work
    /// items after"); 0 when none were left out. The last column says so.
    /// </summary>
    public int DoneCutoffDays { get; init; }

    /// <summary>
    /// Where <see cref="DoneCutoffDays"/> came from; null until a Kanban board load applied a cutoff, so a tab with
    /// null is not a Kanban board (the cutoff choice does not apply to it).
    /// </summary>
    public DoneCutoffSource? CutoffSource { get; init; }

    /// <summary>
    /// The quick choice of the cutoff for this tab (<c>view.done-cutoff</c>): days, 0 for all, null to follow Jira.
    /// Kept with the remembered tab, so it belongs to the profile and the board of the tab; never written to Jira.
    /// </summary>
    public int? DoneCutoffChoice { get; init; }

    /// <summary>
    /// The open and future sprints of a Scrum backlog or Sprint view tab (the latter for Complete, #504), ordered as Jira lists them: the metadata for the section
    /// headers (dates, goal) and the sprints that have no loaded issue yet. Empty until the sprint list arrives.
    /// </summary>
    public IReadOnlyList<Sprint> Sprints { get; init; } = [];

    /// <summary>
    /// The keys of the collapsed sections of a Scrum backlog tab (<c>BacklogSections</c>). Scoped to the tab, like the
    /// selection; a reload keeps them.
    /// </summary>
    public IReadOnlyList<string> CollapsedSections { get; init; } = [];

    /// <summary>
    /// The id of the empty sprint section of a Scrum backlog the cursor stands on (it has no row to stand on), null
    /// when the cursor is on a row. <see cref="Selected"/> keeps the row after the section, but no row action targets
    /// it while this is set (<c>EmptySprintFocus</c>).
    /// </summary>
    public long? FocusedSprintId { get; init; }

    /// <summary>
    /// The quick filters the board offers (<c>GET board/{id}/quickfilter</c>), in board order; read the first time a
    /// filter key is used on the tab (#192).
    /// </summary>
    public IReadOnlyList<QuickFilter> QuickFilters { get; init; } = [];

    /// <summary>The quick filters were read, so an empty <see cref="QuickFilters"/> means the board has none.</summary>
    public bool QuickFiltersKnown { get; init; }

    /// <summary>The ids of the quick filters switched on; their JQL narrows what the board source loads.</summary>
    public IReadOnlyList<long> ActiveQuickFilters { get; init; } = [];

    /// <summary>How the columns layout groups the cards into lanes (#192); one lane is shown at a time.</summary>
    public SwimlaneKind Swimlane { get; init; }

    /// <summary>
    /// The keys of the cards whose sub-task list is expanded in the columns layout (<c>z a</c>, #520); kept per tab.
    /// </summary>
    public IReadOnlyList<string> ExpandedSubtasks { get; init; } = [];

    /// <summary>The key of the lane shown (<see cref="SwimlaneLane.Key"/>); null or a vanished one shows the first.</summary>
    public string? Lane { get; init; }

    /// <summary>
    /// The tab came back from the remembered tabs and has not loaded from the network yet: a board that no longer
    /// exists drops it instead of showing an error.
    /// </summary>
    public bool Restored { get; init; }

    /// <summary>
    /// The first page waits for the quick filters of the board (a restored tab with filters on): their clauses are
    /// part of the first request.
    /// </summary>
    public bool AwaitingFilters { get; init; }

    /// <summary>
    /// The name the user gave the tab (<c>tab.rename</c>, #574); null shows the source label. Local data, kept with the
    /// remembered tab definitions.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>The rows held, counting <see cref="PendingRows"/>.</summary>
    public int HeldRows => Rows.Count + (PendingRows?.Count ?? 0);

    /// <summary>A tab with no rows that starts loading with <paramref name="requestId"/>.</summary>
    public static TabState Opened(TabId id, ViewDefinition definition, long requestId, long sequence, int visibleRows)
    {
        return new TabState(
            id,
            definition,
            [],
            null,
            false,
            0,
            null,
            0,
            visibleRows,
            new Loading(LoadReason.Open),
            requestId,
            sequence,
            false,
            null,
            null,
            false,
            0);
    }
}
