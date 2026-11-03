using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
using Geeratya.Core.Text;

namespace Geeratya.Core.State;

/// <summary>
/// The editing keys of the single-line prompts that live in <see cref="AppState"/> (the command palette, the source
/// picker and the jump prompt): the action ids map to <see cref="PromptOperation"/> and the one editor,
/// <see cref="PromptText"/>, does the work. Form fields and the help filter call the same editor from their own
/// reducers. Pure.
/// </summary>
public static class PromptEditing
{
    /// <summary>The prompt operation an action stands for, or null when the action is not a prompt editing key.</summary>
    public static PromptOperation? OperationOf(string? action)
    {
        return action switch
        {
            ActionIds.EditorLeft => PromptOperation.Left,
            ActionIds.EditorRight => PromptOperation.Right,
            ActionIds.EditorRowStart => PromptOperation.LineStart,
            ActionIds.EditorRowEnd => PromptOperation.LineEnd,
            ActionIds.EditorWordLeft => PromptOperation.WordLeft,
            ActionIds.EditorWordRight => PromptOperation.WordRight,
            ActionIds.EditorDelete => PromptOperation.Delete,
            ActionIds.EditorDeleteWordBefore => PromptOperation.DeleteWordBefore,
            ActionIds.EditorDeleteWordAfter => PromptOperation.DeleteWordAfter,
            ActionIds.EditorDeleteToStart => PromptOperation.DeleteToStart,
            _ => null,
        };
    }

    /// <summary>Applies an editing key to the open palette, picker or jump prompt; other states stay as they are.</summary>
    public static AppState Apply(AppState state, PromptOperation operation)
    {
        return Edit(state, prompt => prompt.Apply(operation));
    }

    /// <summary>Typed or pasted text goes in at the caret of the open palette, picker or jump prompt.</summary>
    public static AppState Insert(AppState state, string text)
    {
        return Edit(state, prompt => prompt.Insert(text));
    }

    /// <summary>Backspace in the open palette, picker or jump prompt.</summary>
    public static AppState Backspace(AppState state)
    {
        return Apply(state, PromptOperation.Backspace);
    }

    // A changed text sends the highlight back to the first row (the filter changed); a caret move leaves it.
    private static AppState Edit(AppState state, Func<PromptText, PromptText> change)
    {
        if (state.Mode == InputMode.Command)
        {
            var edited = change(new PromptText(state.PaletteText, state.PaletteBack));
            var selected = edited.Text == state.PaletteText ? state.PaletteSelected : 0;

            return state with { PaletteText = edited.Text, PaletteBack = edited.Back, PaletteSelected = selected };
        }

        if (state.Jump is { } jump)
        {
            var edited = change(new PromptText(jump.Text, jump.Back));

            if (edited.Text == jump.Text)
            {
                return state with { Jump = jump with { Back = edited.Back } };
            }

            return state with { Jump = new JumpState(edited.Text, 0, null) { Back = edited.Back } };
        }

        if (state.Picker is { } picker)
        {
            var edited = change(new PromptText(picker.Text, picker.Back));

            if (edited.Text == picker.Text)
            {
                return state with { Picker = picker with { Back = edited.Back } };
            }

            return state with { Picker = Changed(picker, edited) };
        }

        return state;
    }

    private static SourcePickerState Changed(SourcePickerState picker, PromptText edited)
    {
        return picker with { Text = edited.Text, Back = edited.Back, Selected = 0, AutoOpen = false, Notice = null };
    }
}
