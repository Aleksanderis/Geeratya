using Geeratya.Core.Text;

namespace Geeratya.Core.Forms;

/// <summary>
/// What a presentation tells <see cref="FieldRows"/> about how to draw a field: the width, the role of the value
/// text and the layout. Everything about the field itself comes from the field.
/// </summary>
/// <param name="Width">Columns for the value.</param>
/// <param name="Role">The role of the value text (a blocks pane uses its own).</param>
public sealed record FieldLook(int Width, TextRole Role = TextRole.Editable)
{
    /// <summary>The field is being edited: its caret is drawn.</summary>
    public bool Editing { get; init; }

    /// <summary>One flowing line of values (box, dropdown) or one row per value (the Details blocks).</summary>
    public FieldLayout Layout { get; init; } = FieldLayout.Inline;

    /// <summary>The rows of a paragraph shown while it is edited (the window follows the caret).</summary>
    public int Rows { get; init; } = 1;

    /// <summary>A paragraph shows all its rows, edited or not.</summary>
    public bool Expanded { get; init; }

    /// <summary>The text of an empty value in place of the shared empty word (for example the title's question).</summary>
    public string? Placeholder { get; init; }

    /// <summary>The field has the focus (edited or not): its placeholder is hidden.</summary>
    public bool Focused { get; init; }

    /// <summary>The edited text is drawn as an input box on the field fill (the in-place editors).</summary>
    public bool Boxed { get; init; }
}
