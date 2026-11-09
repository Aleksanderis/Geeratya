namespace Geeratya.Core.Startup;

/// <summary>
/// One tab as it is kept in the user's file: the source, the layout and the board options, never rows, tokens or a
/// site. Kinds and layouts are words, so an entry written by a newer version is skipped instead of rejecting the file.
/// </summary>
/// <param name="Source">The <see cref="Views.ViewSourceKind"/> by name.</param>
/// <param name="SourceValue">The JQL text, filter id, board id or sprint source, depending on the source.</param>
/// <param name="Layout">The <see cref="Views.ViewLayout"/> by name.</param>
/// <param name="Title">The tab title (for boards the board name).</param>
/// <param name="QuickFilters">The ids of the quick filters switched on.</param>
/// <param name="Swimlane">The <see cref="Boards.SwimlaneKind"/> by name.</param>
/// <param name="Lane">The key of the lane shown, or null.</param>
/// <param name="Name">The name the user gave the tab (<c>tab.rename</c>), or null for the source label.</param>
/// <param name="DoneCutoff">The quick choice of the Done cutoff of a Kanban board (<c>view.done-cutoff</c>): days,
/// 0 for all, or null to follow Jira.</param>
/// <param name="Stacked">The lanes' columns are open at once, stacked (<c>view.swimlane-stack</c>, #891); null for off.</param>
public sealed record RememberedTab(
    string Source,
    string SourceValue,
    string Layout,
    string? Title,
    IReadOnlyList<long>? QuickFilters = null,
    string? Swimlane = null,
    string? Lane = null,
    string? Name = null,
    int? DoneCutoff = null,
    bool? Stacked = null);
