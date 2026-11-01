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
public sealed record LoadViewPageEffect(
    TabId Tab,
    ViewDefinition Definition,
    PageCursor? Cursor,
    long RequestId,
    LoadReason Reason,
    bool Background) : Effect;
