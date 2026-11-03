namespace Geeratya.Core.Text;

/// <summary>
/// What a span sits on. A flag set: a box under another overlay is both <see cref="Surface"/> and <see cref="Dim"/>.
/// </summary>
[Flags]
public enum SpanGround
{
    /// <summary>The page background: the view.</summary>
    Page = 0,

    /// <summary>The raised fill of an overlay box.</summary>
    Surface = 1,

    /// <summary>Muted because an overlay floats over it.</summary>
    Dim = 2,
}
