using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;

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
    bool Cold)
{
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
            false);
    }
}
