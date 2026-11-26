using Geeratya.Core.Editing;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;

namespace Geeratya.Core.Forms;

public static partial class FormReducer
{
    /// <summary>
    /// A click or double-click on the text of the field being edited (Summary, Description, any typed text field): the
    /// caret goes under the pointer or the word there is selected. Nothing happens while the form saves, asks to
    /// discard, is in its navigate state or when that field is not the one being edited.
    /// </summary>
    public static ReduceResult Pointed(AppState state, TextPointed pointed)
    {
        return Pointing(
            state,
            pointed.Index,
            buffer =>
            {
                var index = TextPointing.IndexInLine(buffer, pointed.Line, pointed.Cell, pointed.Width);

                return pointed.Word ? TextPointing.Word(buffer, index) : TextPointing.Placed(buffer, index);
            });
    }

    /// <summary>A drag over the text of the field being edited: the text between the press and the pointer is selected.</summary>
    public static ReduceResult Dragged(AppState state, TextDragged dragged)
    {
        return Pointing(
            state,
            dragged.Index,
            buffer =>
            {
                var from = buffer.Anchor is { } kept && dragged.Continued
                    ? kept
                    : TextPointing.IndexInLine(buffer, dragged.FromLine, dragged.FromCell, dragged.Width);
                var to = TextPointing.IndexInLine(buffer, dragged.ToLine, dragged.ToCell, dragged.Width);

                return TextPointing.Dragged(buffer, from, to);
            });
    }

    private static ReduceResult Pointing(AppState state, int index, Func<TextBuffer, TextBuffer> point)
    {
        if (ActiveTab(state) is not { Form: { } form } tab || form.IsSaving || form.Navigating
            || state.Mode == InputMode.Confirm || index != form.Focus || index < 0 || index >= form.Fields.Count)
        {
            return Result(state);
        }

        var field = form.Fields[index];
        var moved = FormEditor.Pointed(field, point);

        if (ReferenceEquals(moved, field))
        {
            return Result(state);
        }

        return Result(Replace(state, tab, form with { Fields = WithField(form.Fields, index, moved) }));
    }
}
