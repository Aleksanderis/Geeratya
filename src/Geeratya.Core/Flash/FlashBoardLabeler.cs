using Geeratya.Core.Boards;
using Geeratya.Core.Lists;
using Geeratya.Core.Text;
using Geeratya.Core.Views;

namespace Geeratya.Core.Flash;

/// <summary>
/// The flash jump labels of the columns layout: every card in view, in every column that is drawn, whose key or
/// summary (as the card shows it) contains the typed text gets a letter. The letter takes the cell before the type
/// name (the set mark), which no text match can cover, so it always fits. Nearest to the cursor first. Pure; the
/// measures are the ones <c>ColumnsBodyRenderer</c> draws with (<see cref="BoardCards"/>, <see cref="BoardWindows"/>).
/// </summary>
public static class FlashBoardLabeler
{
    private const int Margin = BoardCards.CardMargin;
    private const int Borders = 2;
    private const int Padding = 2;
    private const int MinimumBoxWidth = BoardCards.MinimumBoxWidth;
    private const int ChildIndent = BoardCards.ChildIndent;
    private const int KeyLineReserve = 4;

    /// <summary>The cells before the card text in a full card: the margin and the side border.</summary>
    public const int FullCardLabelColumn = Margin + 1;

    /// <summary>
    /// The labels for <paramref name="text"/> in a body of <paramref name="width"/> by <paramref name="height"/>.
    /// With stacked lanes (#891) every drawn lane is searched, the current lane's labels first so its cards get the
    /// first letters.
    /// </summary>
    public static IReadOnlyList<FlashLabel> Compute(TabState tab, string text, int width, int height)
    {
        if (text.Length == 0 || tab.Rows.Count == 0)
        {
            return [];
        }

        var found = new List<FlashLabel>();
        var blocked = new HashSet<char>();
        var current = ColumnsNavigation.CurrentLaneKey(tab);

        foreach (var region in LaneRegions.Regions(tab).Where(region => !region.Folded).OrderBy(region => region.Key == current ? 0 : 1))
        {
            found.AddRange(LabelsOf(tab, region, text, width, height, blocked));
        }

        FlashLabeler.AssignLetters(found, blocked);

        return found;
    }

    private static List<FlashLabel> LabelsOf(
        TabState tab,
        LaneRegion region,
        string text,
        int width,
        int height,
        HashSet<char> blocked)
    {
        var boardHeight = Math.Min(region.BoardBodyRows, height - region.BoardTop);
        var columns = ColumnsNavigation.ColumnsOfLane(tab, region.Key);

        if (columns.Count == 0 || !ListViewColumns.IsFramed(width, boardHeight))
        {
            return [];
        }

        var boardRows = region.BoardRows;
        var visible = BoardCards.Visible(boardRows, ListLayout.HeaderRows);
        var cursor = ColumnsNavigation.CursorOfLane(tab, region, columns, width);
        var window = BoardWindows.For(columns.Count, cursor.Column, width);
        var compact = BoardCards.IsCompact(boardRows, ListLayout.HeaderRows);
        var found = new List<FlashLabel>();

        for (var position = 0; position < window.Count; position++)
        {
            var column = window.First + position;
            var cell = window.CellWidth(position);
            var group = columns[column];

            foreach (var card in CardsInView(group, cursor.Scrolls[column], cell, boardRows, compact, visible))
            {
                var fields = FieldsOf(group, card, cell, compact);

                foreach (var field in fields)
                {
                    FlashLabeler.BlockContinuations(field, text, blocked);
                }

                if (fields.Any(field => field.Contains(text, StringComparison.OrdinalIgnoreCase)))
                {
                    found.Add(LabelOf(group, column, card, compact));
                }
            }
        }

        found.Sort((left, right) => Compare(left, right, cursor));

        return found;
    }

    private static IEnumerable<int> CardsInView(
        BoardColumnGroup group,
        int first,
        int cell,
        int boardRows,
        bool compact,
        int visible)
    {
        if (compact)
        {
            return Enumerable.Range(first, Math.Max(0, Math.Min(group.Rows.Count, first + visible) - first));
        }

        var rows = BoardCards.CardRows(boardRows, ListLayout.HeaderRows);

        return BoardColumnLayout.Place(group, first, cell, rows).Select(static placed => placed.Card);
    }

    private static FlashLabel LabelOf(BoardColumnGroup group, int column, int card, bool compact)
    {
        var offset = compact ? 0 : FullCardLabelColumn + (group.IsNested(card) ? ChildIndent : 0);

        return new FlashLabel(card, 0, 0, offset, FlashLabeler.Eligible) { Column = column, Key = group.Rows[card].Key };
    }

    /// <summary>The texts of a card as drawn, a match must lie in one of them.</summary>
    private static List<string> FieldsOf(BoardColumnGroup group, int card, int cell, bool compact)
    {
        var issue = group.Rows[card];
        var nested = group.IsNested(card);

        if (compact)
        {
            var line = CardText.Compact(issue, false, nested, group.IsNested(card + 1), cell);

            return line.Length > 1 ? [line[1..]] : [];
        }

        var box = cell - (2 * Margin);

        if (box < MinimumBoxWidth)
        {
            return [];
        }

        var room = box - Borders - (nested ? ChildIndent : 0);
        var key = DisplayWidth.Fit(issue.Key, room - KeyLineReserve);

        var lines = CardText.TwoLines(issue.Summary, room - Padding);

        return lines.Second.Length > 0 ? [key, lines.First, lines.Second] : [key, lines.First];
    }

    private static int Compare(FlashLabel left, FlashLabel right, BoardCursor cursor)
    {
        var byColumn = Math.Abs(left.Column - cursor.Column).CompareTo(Math.Abs(right.Column - cursor.Column));

        if (byColumn != 0)
        {
            return byColumn;
        }

        var selected = cursor.Cards[left.Column];
        var byCard = Math.Abs(left.Row - selected).CompareTo(Math.Abs(right.Row - cursor.Cards[right.Column]));

        if (byCard != 0)
        {
            return byCard;
        }

        var byPosition = left.Column.CompareTo(right.Column);

        return byPosition != 0 ? byPosition : left.Row.CompareTo(right.Row);
    }
}
