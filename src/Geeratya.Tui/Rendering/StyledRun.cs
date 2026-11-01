namespace Geeratya.Tui.Rendering;

/// <summary>A piece of text with the style it is drawn in for one colour tier.</summary>
public sealed record StyledRun(string Text, ResolvedStyle Style);
