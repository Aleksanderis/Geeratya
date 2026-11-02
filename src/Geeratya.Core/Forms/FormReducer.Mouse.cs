using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Forms;

public static partial class FormReducer
{
    /// <summary>
    /// A click inside the open form: on a field it focuses that field (<c>Tab</c> until there), on a row of the type
    /// list or of the suggestion popup it takes that row (<c>Enter</c> on it). Nothing happens while the form saves,
    /// asks to discard or has no such row, exactly as the keys do nothing then.
    /// </summary>
    public static ReduceResult Clicked(AppState state, OverlayRowClicked clicked)
    {
        if (ActiveTab(state) is not { Form: { } form } tab || form.IsSaving || form.DiscardPending)
        {
            return Result(state);
        }

        if (clicked.Target == MouseTarget.FormChoice && form.IsChoosingType)
        {
            return ChosenType(state, tab, form, clicked.Index);
        }

        if (form.IsChoosingType)
        {
            return Result(state);
        }

        if (clicked.Target == MouseTarget.FormChoice)
        {
            return ChosenSuggestion(state, tab, form, clicked);
        }

        return FocusedField(state, tab, form, clicked.Index);
    }

    private static ReduceResult ChosenType(AppState state, TabState tab, FormState form, int index)
    {
        var count = form.TypeChoices?.Count ?? 0;

        if (index < 0 || index >= count)
        {
            return Result(state);
        }

        return CreateFormReducer.ChoosingType(
            Replace(state, tab, form with { TypeCursor = index }),
            tab,
            form with { TypeCursor = index },
            ActionIds.FormNext);
    }

    private static ReduceResult ChosenSuggestion(
        AppState state,
        TabState tab,
        FormState form,
        OverlayRowClicked clicked)
    {
        if (form.Suggest is not { } popup || clicked.Index < 0 || clicked.Index >= popup.Items.Count)
        {
            return Result(state);
        }

        var moved = form with { Suggest = popup with { Cursor = clicked.Index } };
        var taken = Replace(state, tab, moved);
        var handled = Suggesting(taken, tab, moved, moved.Suggest!, ActionIds.FormNext, clicked.At);

        return Choosing(handled ?? Result(state), tab.Id);
    }

    private static ReduceResult FocusedField(AppState state, TabState tab, FormState form, int index)
    {
        if (index < 0 || index >= form.Fields.Count || index == form.Focus || !form.Fields[index].IsEditable)
        {
            return Result(state);
        }

        var moved = Result(Replace(state, tab, form with { Focus = index }));

        return Choosing(Unsuggested(moved, tab.Id), tab.Id);
    }
}
