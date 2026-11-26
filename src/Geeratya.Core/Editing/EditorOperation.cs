namespace Geeratya.Core.Editing;

/// <summary>
/// An editing key of any text surface (<see cref="TextBuffer.Apply"/>): the one list the palette, pickers, filters,
/// form fields, the profile form and the comment editor map their editor actions onto (<see cref="EditorActions"/>).
/// </summary>
public enum EditorOperation
{
    Left,
    Right,
    Up,
    Down,
    PageUp,
    PageDown,
    RowStart,
    RowEnd,
    WordLeft,
    WordRight,
    Backspace,
    Delete,
    DeleteWordBefore,
    DeleteWordAfter,
    DeleteToStart,
    DeleteToEnd,
    SelectLeft,
    SelectRight,
    SelectUp,
    SelectDown,
    SelectRowStart,
    SelectRowEnd,
    SelectWordLeft,
    SelectWordRight,
    SelectAll,
}
