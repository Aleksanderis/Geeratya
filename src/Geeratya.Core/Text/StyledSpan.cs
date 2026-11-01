namespace Geeratya.Core.Text;

/// <summary>A run of text that carries one role.</summary>
public readonly record struct StyledSpan(string Text, TextRole Role);
