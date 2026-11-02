namespace Geeratya.Core.Cache;

/// <summary>
/// The JSON shape of one ADF node in a cached issue file. One flat shape serves every node type, so no
/// polymorphic JSON is needed; <see cref="Kind"/> is the ADF type name (or <c>unknown</c>) and the other members
/// carry that type's attributes, as <see cref="AdfCacheMapper"/> spells out per kind.
/// </summary>
/// <param name="Kind">The ADF node type name; <c>unknown</c> for a node the model does not know.</param>
/// <param name="Text">The node's own text (text, emoji glyph, status text, mention text, unknown text).</param>
/// <param name="Value">The first string attribute (language, panel type, title, layout, URL, id, colour...).</param>
/// <param name="Second">The second string attribute (media type).</param>
/// <param name="Third">The third string attribute (media collection).</param>
/// <param name="Alt">The alternative text of a media node.</param>
/// <param name="Number">The numeric attribute (level, order, column span, timestamp, media width).</param>
/// <param name="Height">The height of a media node.</param>
/// <param name="Marks">The marks of a text node.</param>
/// <param name="Children">The child nodes.</param>
public sealed record CachedAdfNode(
    string Kind,
    string? Text,
    string? Value,
    string? Second,
    string? Third,
    string? Alt,
    long? Number,
    int? Height,
    List<CachedAdfMark>? Marks,
    List<CachedAdfNode>? Children);
