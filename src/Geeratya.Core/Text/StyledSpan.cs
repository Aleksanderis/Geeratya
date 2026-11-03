namespace Geeratya.Core.Text;

/// <summary>
/// A run of text that carries one role and where it sits: on the page (the default), on the raised surface of an
/// overlay box, or dimmed because an overlay floats over it (<c>docs/ux-look.md</c>, sections 6 and 7).
/// </summary>
public readonly record struct StyledSpan(string Text, TextRole Role, SpanGround Ground = SpanGround.Page);
