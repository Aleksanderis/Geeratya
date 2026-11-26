using Geeratya.Core.Keymap;

namespace Geeratya.Core.Editing;

/// <summary>The one mapping of editor action ids onto <see cref="EditorOperation"/>, for every surface. Pure.</summary>
public static class EditorActions
{
    /// <summary>
    /// The operation an action stands for, or null when it is not an editing key. The page keys are for a text with
    /// lines (<paramref name="withLines"/>): a single-line surface does not take them.
    /// </summary>
    public static EditorOperation? OperationOf(string? action, bool withLines = false)
    {
        return action switch
        {
            ActionIds.EditorLeft => EditorOperation.Left,
            ActionIds.EditorRight => EditorOperation.Right,
            ActionIds.EditorRowStart => EditorOperation.RowStart,
            ActionIds.EditorRowEnd => EditorOperation.RowEnd,
            ActionIds.EditorWordLeft => EditorOperation.WordLeft,
            ActionIds.EditorWordRight => EditorOperation.WordRight,
            ActionIds.EditorDelete => EditorOperation.Delete,
            ActionIds.EditorDeleteWordBefore => EditorOperation.DeleteWordBefore,
            ActionIds.EditorDeleteWordAfter => EditorOperation.DeleteWordAfter,
            ActionIds.EditorDeleteToStart => EditorOperation.DeleteToStart,
            ActionIds.EditorSelectLeft => EditorOperation.SelectLeft,
            ActionIds.EditorSelectRight => EditorOperation.SelectRight,
            ActionIds.EditorSelectRowStart => EditorOperation.SelectRowStart,
            ActionIds.EditorSelectRowEnd => EditorOperation.SelectRowEnd,
            ActionIds.EditorSelectWordLeft => EditorOperation.SelectWordLeft,
            ActionIds.EditorSelectWordRight => EditorOperation.SelectWordRight,
            ActionIds.EditorSelectAll => EditorOperation.SelectAll,
            ActionIds.EditorSelectUp when withLines => EditorOperation.SelectUp,
            ActionIds.EditorSelectDown when withLines => EditorOperation.SelectDown,
            ActionIds.EditorPageUp when withLines => EditorOperation.PageUp,
            ActionIds.EditorPageDown when withLines => EditorOperation.PageDown,
            _ => null,
        };
    }
}
