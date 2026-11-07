using Geeratya.Core.Editing;
using Geeratya.Core.State;

namespace Geeratya.Core.Comments;

internal static partial class CommentEditing
{
    private const string SavingText = "Wait for the save to finish before opening the outside editor.";
    private const string TooLongText =
        "The outside editor's text is longer than 30000 characters; the text is unchanged.";

    /// <summary><c>editor.external</c>: asks the host to edit the text in the user's editor.</summary>
    public static ReduceResult External(AppState state)
    {
        if (state.Editor is not { } editor)
        {
            return Result(state);
        }

        if (editor.IsSaving)
        {
            return Say(state, SavingText);
        }

        var ask = new EditExternallyEffect(editor.Tab, editor.Key, editor.CommentId, editor.Buffer.Text);

        return new ReduceResult(WithEditor(state, editor with { Error = null, Suggest = null }), [ask]);
    }

    /// <summary>
    /// Takes the text back from the outside editor into the open editor (cursor at its end). A reply for another
    /// editor, or one that arrives while a save runs, is dropped; a failure is shown in the editor, text intact.
    /// </summary>
    public static AppState ExternalFinished(AppState state, ExternalEditFinished finished)
    {
        if (state.Editor is not { IsSaving: false } editor || !IsEditorOf(editor, finished))
        {
            return state;
        }

        if (finished.Error is not null)
        {
            return WithEditor(state, editor with { Error = $"The outside editor failed: {Clean(finished.Error)}" });
        }

        if (finished.Text is null)
        {
            return state;
        }

        var text = PasteText.Lines(finished.Text).TrimEnd('\n');

        if (text.Length > TextBuffer.MaxLength)
        {
            return WithEditor(state, editor with { Error = TooLongText });
        }

        if (string.Equals(text, editor.Buffer.Text, StringComparison.Ordinal))
        {
            return state;
        }

        return WithEditor(state, editor with { Buffer = TextBuffer.Of(text), Error = null, Dismissed = -1 });
    }

    private static bool IsEditorOf(CommentEditor editor, ExternalEditFinished finished)
    {
        return editor.Tab == finished.Tab
            && editor.Key == finished.Key
            && editor.CommentId == finished.CommentId;
    }
}
