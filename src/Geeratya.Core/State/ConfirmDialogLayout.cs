namespace Geeratya.Core.State;

/// <summary>
/// What the Confirm dialog shows for a request and how much room it needs: the question wrapped to the box, the
/// warning sentences (the ones that say Jira cannot undo it) on their own lines, and the answer rows. The renderer
/// draws this layout and the reducer asks it whether the dialog fits, so both agree. Pure.
/// </summary>
public sealed record ConfirmDialogLayout(
    int BoxWidth,
    IReadOnlyList<string> Question,
    IReadOnlyList<string> Warning,
    string YesLabel,
    string Title = ConfirmDialog.Title,
    bool Notice = false)
{
    /// <summary>Rows of the box with its two edges: question, a gap and the warning, a gap, then the two answers (a notice has only the close row).</summary>
    public int Height => 2 + Question.Count + (Warning.Count > 0 ? Warning.Count + 1 : 0) + 1 + (Notice ? 1 : 2);

    /// <summary>The row of the <c>y</c> answer, counted from the top edge of the box.</summary>
    public int YesRow => Height - 3;

    /// <summary>The row of the <c>n</c> answer, counted from the top edge of the box.</summary>
    public int NoRow => Notice ? -1 : Height - 2;

    /// <summary>Columns of text between the walls and their padding.</summary>
    public int TextColumns => BoxWidth - 4;
}
