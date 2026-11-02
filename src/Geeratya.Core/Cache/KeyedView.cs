namespace Geeratya.Core.Cache;

/// <summary>A cached view together with the key it was filed under.</summary>
public sealed record KeyedView(CacheKey Key, CachedView View);
