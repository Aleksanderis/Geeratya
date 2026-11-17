namespace Geeratya.Core.Forms;

/// <summary>How <see cref="FieldRows"/> lays out the chosen values of a choice, labels, person or parent field.</summary>
public enum FieldLayout
{
    /// <summary>The values flow on one wrapped line, the typed filter after them.</summary>
    Inline,

    /// <summary>One row per value (the filled blocks of the Details pane), the typed filter as the last row.</summary>
    Items,
}
