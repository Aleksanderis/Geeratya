namespace Geeratya.Core.Cache;

/// <summary>The JSON shape of an ADF mark: its type name and the one attribute it may carry (link, colour).</summary>
/// <param name="Kind">The ADF mark type name; <c>unknown</c> for a mark the model does not know.</param>
/// <param name="Value">The link address, colour, sub/sup kind or unknown type name.</param>
public sealed record CachedAdfMark(string Kind, string? Value);
