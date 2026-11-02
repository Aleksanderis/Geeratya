namespace Geeratya.Core.Views;

/// <summary>A view is a source (where issues come from) plus a layout (how they are shown).</summary>
/// <param name="Source">The kind of source.</param>
/// <param name="SourceValue">The JQL text, filter id or board id, depending on <paramref name="Source"/>.</param>
/// <param name="Layout">How the issues are shown.</param>
/// <param name="Title">The tab title (for boards the board name).</param>
public sealed record ViewDefinition(ViewSourceKind Source, string SourceValue, ViewLayout Layout, string Title);
