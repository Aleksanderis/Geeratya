using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Boards;

/// <summary>
/// The rows of a full card (<c>docs/internal/ux-screens.md</c>, "Board card"): the summary on one or two lines, a blank row,
/// the label chips on one or two rows followed by a blank row (none without labels), and the key row, inside a top
/// and a bottom border. So the height follows the content, from five to nine rows; a member of a shared frame has no
/// own top border (a child) and a divider instead of a bottom border. The renderer, the hit test and the flash
/// labeler agree on it through here. Pure.
/// </summary>
public static class BoardCardPlan
{
    /// <summary>The most rows a full card takes: top, two summary lines, blank, two label rows, blank, key, bottom.</summary>
    public const int MaxRows = 9;

    /// <summary>The cells of margin, side borders and padding around the text of a card of its own.</summary>
    private const int FrameCells = (2 * BoardCards.CardMargin) + 4;

    /// <summary>The cells available to the text of a card in a column cell of <paramref name="cellWidth"/> cells.</summary>
    public static int TextWidth(int cellWidth, bool nested)
    {
        return Math.Max(0, cellWidth - FrameCells - (nested ? BoardCards.ChildIndent : 0));
    }

    /// <summary>The rows of the card for <paramref name="issue"/> in a column cell of <paramref name="cellWidth"/>.</summary>
    public static IReadOnlyList<CardRow> Rows(IssueSummary issue, CardGroupPart part, int cellWidth)
    {
        var nested = part is CardGroupPart.Middle or CardGroupPart.Last;
        var text = TextWidth(cellWidth, nested);
        var rows = new List<CardRow>();

        if (part is CardGroupPart.None or CardGroupPart.First)
        {
            rows.Add(new CardRow(CardRowKind.Top, 0));
        }

        for (var line = 0; line < SummaryLines(issue, text); line++)
        {
            rows.Add(new CardRow(CardRowKind.Summary, line));
        }

        rows.Add(new CardRow(CardRowKind.Blank, 0));

        var labelRows = CardLabelLayout.Rows(issue.Labels, text).Count;

        for (var row = 0; row < labelRows; row++)
        {
            rows.Add(new CardRow(CardRowKind.Labels, row));
        }

        if (labelRows > 0)
        {
            rows.Add(new CardRow(CardRowKind.Blank, 1));
        }

        rows.Add(new CardRow(CardRowKind.Key, 0));
        rows.Add(new CardRow(part == CardGroupPart.None ? CardRowKind.Bottom : CardRowKind.Divider, 0));

        return rows;
    }

    /// <summary>
    /// The number of rows <see cref="Rows"/> would return, without building them (the scroll maths asks for it per
    /// card).
    /// </summary>
    public static int Height(IssueSummary issue, CardGroupPart part, int cellWidth)
    {
        var nested = part is CardGroupPart.Middle or CardGroupPart.Last;
        var text = TextWidth(cellWidth, nested);
        var labelRows = CardLabelLayout.Rows(issue.Labels, text).Count;

        // Summary lines, the blank row, the key row and the bottom border or divider.
        var rows = SummaryLines(issue, text) + 3;

        if (part is CardGroupPart.None or CardGroupPart.First)
        {
            rows++;
        }

        return labelRows > 0 ? rows + labelRows + 1 : rows;
    }

    /// <summary>The summary lines the card shows in <paramref name="text"/> cells: one, or two when it wraps.</summary>
    public static int SummaryLines(IssueSummary issue, int text)
    {
        return CardText.TwoLines(issue.Summary, text).Second.Length > 0 ? 2 : 1;
    }
}
