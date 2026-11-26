namespace Geeratya.Core.State;

/// <summary>
/// A drag over the text being edited in a selecting text area: the text between where the button went down and where
/// the pointer is now is selected, the caret at the pointer. Sent on every move and on the release, so it is idempotent.
/// </summary>
/// <param name="Index">The form field edited, 0 for the comment editor.</param>
/// <param name="FromLine">The line (or visual row) where the button went down.</param>
/// <param name="FromCell">The column of that press in the text as drawn.</param>
/// <param name="ToLine">The line (or visual row) under the pointer.</param>
/// <param name="ToCell">The column of the pointer in the text as drawn.</param>
/// <param name="Width">The columns the text is drawn in.</param>
/// <param name="Continued">
/// Whether an earlier move of this drag already set the anchor: it stays, because the text is drawn around its caret
/// and the press cell no longer maps to the same character once the caret moved.
/// </param>
public sealed record TextDragged(
    int Index,
    int FromLine,
    int FromCell,
    int ToLine,
    int ToCell,
    int Width,
    bool Continued) : Command;
