using Geeratya.Core.Comments;
using Geeratya.Core.Keymap;

namespace Geeratya.Core.State;

/// <summary>
/// The discard question of every editor (a form, the comment editor, the profile form): one <see cref="ConfirmRequest"/>
/// whose <c>y</c> throws the text away and whose <c>n</c>, <c>q</c> or <c>Esc</c> goes back to the editor with it as
/// it was; <c>Enter</c> never answers it (<c>docs/internal/ui-consistency.md</c>, component 3). Pure.
/// </summary>
public static class DiscardPrompts
{
    /// <summary>The action of the discard question of a field form.</summary>
    public const string FormAction = "form.discard";

    /// <summary>The action of the discard question of the comment editor.</summary>
    public const string CommentAction = "comment.discard";

    /// <summary>True when <paramref name="request"/> is a discard question of an editor.</summary>
    public static bool Is(ConfirmRequest? request)
    {
        return request?.ActionOnConfirm is FormAction or CommentEditing.DiscardAction or ProfileReducer.DiscardAction;
    }

    /// <summary>The state with <paramref name="request"/> asked: Confirm mode over the editor that stays open.</summary>
    public static AppState Ask(AppState state, ConfirmRequest request)
    {
        return state with { Mode = InputMode.Confirm, Confirm = request, PendingKeys = string.Empty };
    }

    /// <summary>The answer was no: back to the editor with its text as it was.</summary>
    public static AppState Kept(AppState state)
    {
        return state with { Mode = InputMode.Insert, Confirm = null, PendingKeys = string.Empty };
    }
}
