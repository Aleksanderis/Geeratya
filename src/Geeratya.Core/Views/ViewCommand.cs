using Geeratya.Core.State;
using Geeratya.Jira.Search.Models;
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

/// <summary>Change how a tab shows its rows; everything else is kept.</summary>
public sealed record ViewLayoutChanged(TabId Tab, ViewLayout Layout) : ViewCommand;

/// <summary>The viewport height changed; applies to every tab.</summary>
public sealed record ViewportChanged(int Rows) : ViewCommand;

/// <summary>Load the next page of a tab.</summary>
public sealed record NextPageRequested(TabId Tab) : ViewCommand;

/// <summary>A page arrived; applied only while <paramref name="RequestId"/> is the tab's current request.</summary>
public sealed record ViewPageLoaded(TabId Tab, long RequestId, IssuePage Page, DateTimeOffset At) : ViewCommand;

/// <summary>A load failed; applied only while <paramref name="RequestId"/> is the tab's current request.</summary>
public sealed record ViewLoadFailed(TabId Tab, long RequestId, JiraError Error, DateTimeOffset At) : ViewCommand;
