namespace Geeratya.Core.SavedViews;

/// <summary>
/// One named view as it is kept in the user's file: the source and the layout, never rows, tokens or a site. The kind
/// and the layout are stored as words, so a file written by a newer version with other values is skipped entry by
/// entry instead of being rejected as a whole.
/// </summary>
/// <param name="Name">The name the user gave it.</param>
/// <param name="Source">The <see cref="Views.ViewSourceKind"/> by name (<c>Jql</c>, <c>Board</c>, ...).</param>
/// <param name="SourceValue">The JQL text, filter id, board id or sprint source, depending on the source.</param>
/// <param name="Layout">The <see cref="Views.ViewLayout"/> by name (<c>List</c> or <c>Columns</c>).</param>
public sealed record SavedView(string Name, string Source, string SourceValue, string Layout);
