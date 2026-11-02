using Geeratya.Core.Views;

namespace Geeratya.Core.Cache;

/// <summary>
/// Cached rows for a tab that is still loading. Applied only while <paramref name="RequestId"/> is the tab's
/// current request and the tab has no rows yet, so a network reply that came first is never overwritten.
/// </summary>
public sealed record ViewCacheLoaded(TabId Tab, long RequestId, CachedView View) : ViewCommand;
