namespace Geeratya.Core.Views;

/// <summary>A view is a source (where issues come from) plus a layout (how they are shown).</summary>
public sealed record ViewDefinition(ViewSourceKind Source, string SourceValue, ViewLayout Layout);
