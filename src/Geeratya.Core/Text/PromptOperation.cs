namespace Geeratya.Core.Text;

/// <summary>An editing key of a single-line prompt (<see cref="PromptText.Apply"/>).</summary>
public enum PromptOperation
{
    Left,
    Right,
    LineStart,
    LineEnd,
    WordLeft,
    WordRight,
    Backspace,
    Delete,
    DeleteWordBefore,
    DeleteWordAfter,
    DeleteToStart,
    DeleteToEnd,
}
