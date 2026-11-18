using Geeratya.Core.Editing;
using Geeratya.Core.Flash;
using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Sources;

namespace Geeratya.Core.State;

/// <summary>
/// The editing keys of the single-line prompts that live in <see cref="AppState"/> (the command palette, the source
/// picker and the jump prompt): <see cref="EditorActions"/> maps the action ids to <see cref="EditorOperation"/> and the one editor,
/// <see cref="TextBuffer"/>, does the work. Form fields and the help filter call the same editor from their own
/// reducers. Pure.
/// </summary>
public static class PromptEditing
{
    /// <summary>Applies an editing key to the open palette, picker or jump prompt; other states stay as they are.</summary>
    public static AppState Apply(AppState state, EditorOperation operation)
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
        return Apply(state, EditorOperation.Backspace);
    }

    // A changed text sends the highlight back to the first row (the filter changed); a caret move leaves it.
    private static AppState Edit(AppState state, Func<TextBuffer, TextBuffer> change)
    {
        if (state.Mode == InputMode.Command)
        {
            var edited = change(TextBuffer.FromBack(state.PaletteText, state.PaletteBack));
            var pick = edited.Text == state.PaletteText ? state.PalettePick : PickList.First;

            return state with { PaletteText = edited.Text, PaletteBack = edited.Back, PalettePick = pick };
        }

        if (state.Jump is { } jump)
        {
            var edited = change(TextBuffer.FromBack(jump.Text, jump.Back));

            if (edited.Text == jump.Text)
            {
                return state with { Jump = jump with { Back = edited.Back } };
            }

            return state with { Jump = new JumpState(edited.Text, PickList.First, null) { Back = edited.Back } };
        }

        if (state.FlashJump is { } flash)
        {
            var edited = change(TextBuffer.FromBack(flash.Text, flash.Back));

            return state with { FlashJump = new FlashJumpState(edited.Text) { Back = edited.Back } };
        }

        if (state.Picker is { } picker)
        {
            var edited = change(TextBuffer.FromBack(picker.Text, picker.Back));

            if (edited.Text == picker.Text)
            {
                return state with { Picker = picker with { Back = edited.Back } };
            }

            return state with { Picker = Changed(picker, edited) };
        }

        return state.Form is not null ? ProfileReducer.Edit(state, change) : state;
    }

    private static SourcePickerState Changed(SourcePickerState picker, TextBuffer edited)
    {
        return picker with { Text = edited.Text, Back = edited.Back, Pick = PickList.First, AutoOpen = false, Notice = null };
    }
}
