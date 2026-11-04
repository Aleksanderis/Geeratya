namespace Geeratya.Core.Panes;

/// <summary>One cell of a pane's scrollbar; the rendering layer picks the glyph (<c>▲ █ ░ ▼</c>, ASCII <c>^ # . v</c>).</summary>
public enum ScrollCell
{
    /// <summary>The up arrow at the top.</summary>
    Up,

    /// <summary>The thumb: the part of the content in view.</summary>
    Thumb,

    /// <summary>The track around the thumb.</summary>
    Track,

    /// <summary>The down arrow at the bottom.</summary>
    Down,
}
