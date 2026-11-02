using Geeratya.Core.State;

namespace Geeratya.Core.Cache;

/// <summary>Remember the first page of a view in the active profile's cache; written off the UI thread.</summary>
public sealed record WriteCacheEffect(CacheKey Key, CachedView View) : Effect;

/// <summary>Remember a successfully loaded issue detail in the active profile's cache; written off the UI thread.</summary>
public sealed record WriteIssueCacheEffect(CachedIssue Issue) : Effect;
