namespace Geeratya.Tui.Rendering;

/// <summary>One key hint ready to draw: the key text (from the keymap) and its short label.</summary>
public readonly record struct HintChip(string Key, string Label)
{
    /// <summary>The chip as plain text, <c>key label</c>.</summary>
    public string Text => $"{Key} {Label}";

    /// <summary>The cells the chip takes.</summary>
    public int Width => TextWidth.Of(Text);
}
