namespace Geeratya.Core.Flash;

/// <summary>
/// A visible row that contains the typed text. Columns count from the left edge of the row as drawn (inside the frame).
/// </summary>
/// <param name="Row">The row index in the tab's rows.</param>
/// <param name="MatchColumn">The column where the matched text starts.</param>
/// <param name="MatchWidth">The columns the matched text takes.</param>
/// <param name="LabelColumn">The column of the character that the label letter replaces.</param>
/// <param name="Letter">The key that selects the row, or <see cref="NoLetter"/> when no letter fits or is left.</param>
public readonly record struct FlashLabel(int Row, int MatchColumn, int MatchWidth, int LabelColumn, char Letter)
{
    /// <summary>The value of <see cref="Letter"/> for a match that only Enter can take.</summary>
    public const char NoLetter = '\0';

    /// <summary>The board column of the card in the columns layout; 0 in the list layout and the backlog.</summary>
    public int Column { get; init; }

    /// <summary>The issue key of the card in the columns layout (the row there counts cards of the column); else null.</summary>
    public string? Key { get; init; }

    /// <summary>Whether a key selects this row.</summary>
    public bool HasLetter => Letter != NoLetter;
}
