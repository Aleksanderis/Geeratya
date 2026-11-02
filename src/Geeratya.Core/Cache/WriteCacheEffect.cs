using Geeratya.Core.State;

namespace Geeratya.Core.Cache;

/// <summary>Remember the first page of a view in the active profile's cache; written off the UI thread.</summary>
public sealed record WriteCacheEffect(CacheKey Key, CachedView View) : Effect;

/// <summary>Remember a successfully loaded issue detail in the active profile's cache; written off the UI thread.</summary>
public sealed record WriteIssueCacheEffect(CachedIssue Issue) : Effect;

/// <summary>Forget the cached detail of an issue that is gone or forbidden (404/403) in the active profile's cache.</summary>
public sealed record RemoveIssueCacheEffect(string Key) : Effect;
