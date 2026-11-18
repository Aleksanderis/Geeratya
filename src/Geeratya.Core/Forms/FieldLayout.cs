namespace Geeratya.Core.Forms;

/// <summary>How <see cref="FieldRows"/> lays out the chosen values of a choice, labels, person or parent field.</summary>
public enum FieldLayout
{
    /// <summary>The values flow on one wrapped line, no caret: the open picker draws the input line.</summary>
    Inline,

    /// <summary>One row per value (the filled blocks of the Details pane).</summary>
    Items,
}
