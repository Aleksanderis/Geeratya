namespace Geeratya.Core.Cache;

/// <summary>The JSON shape of an ADF document.</summary>
public sealed record CachedAdfDocument(int Version, bool IsTruncated, List<CachedAdfNode> Content);
