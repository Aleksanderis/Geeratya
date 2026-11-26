namespace Geeratya.Core.State;

/// <summary>
/// A click or double-click on the text being edited in a selecting text area: the caret goes under the pointer, or
/// the word there is selected.
/// </summary>
/// <param name="Index">The form field edited, 0 for the comment editor.</param>
/// <param name="Line">The line (or visual row, in the comment editor) that was hit.</param>
/// <param name="Cell">The column in the text as drawn, from its left edge.</param>
/// <param name="Width">The columns the text is drawn in.</param>
/// <param name="Word">Whether to select the word under the pointer (a double-click) instead of placing the caret.</param>
public sealed record TextPointed(int Index, int Line, int Cell, int Width, bool Word) : Command;
