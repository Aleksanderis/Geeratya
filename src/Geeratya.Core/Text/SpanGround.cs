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

    /// <summary>
    /// On the selected row: the span keeps its own colours over the selection tint; without colour the whole
    /// row is reversed.
    /// </summary>
    Selection = 4,

    /// <summary>On a row of the Visual range: the stronger tint, bold in every tier.</summary>
    Range = 8,

    /// <summary>On the row the cursor is on: underlined in every tier (the moving end of a Visual range).</summary>
    Cursor = 16,
}
