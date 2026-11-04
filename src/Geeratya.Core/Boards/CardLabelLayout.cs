using Geeratya.Core.Text;

namespace Geeratya.Core.Boards;

/// <summary>
/// How the labels of a card are laid out as chips: a filled chip each (the label with one cell of padding on both sides, no brackets: the fill marks it),
/// wrapping from the first row to a second one, then <c>+N</c> for what does not fit. A chip wider than a row is cut with an ellipsis. Pure.
/// </summary>
public static class CardLabelLayout
{
    /// <summary>The most rows the labels take.</summary>
    public const int MaxRows = 2;

    private const int MinCutChip = 4;
    private const int ChipPadding = 2;

    /// <summary>The rows for <paramref name="labels"/> in <paramref name="width"/> cells; empty without labels.</summary>
    public static IReadOnlyList<CardLabelRow> Rows(IReadOnlyList<string> labels, int width)
    {
        if (labels.Count == 0)
        {
            return [];
        }

        var rows = new List<List<CardLabelChip>> { new() };
        var used = 0;
        var placed = 0;

        for (var index = 0; index < labels.Count; index++)
        {
            var chip = Fit(labels, index, rows.Count == MaxRows, width, used);

            if (chip is null && rows.Count < MaxRows && rows[^1].Count > 0)
            {
                rows.Add([]);
                used = 0;
                chip = Fit(labels, index, rows.Count == MaxRows, width, used);
            }

            if (chip is null)
            {
                break;
            }

            used += (rows[^1].Count > 0 ? 1 : 0) + DisplayWidth.Of(chip.Text);
            rows[^1].Add(chip);
            placed++;
        }

        return Close(rows, labels.Count - placed, width);
    }

    private static CardLabelChip? Fit(IReadOnlyList<string> labels, int index, bool last, int width, int used)
    {
        var more = labels.Count - index - 1;
        var reserve = last && more > 0 ? 1 + DisplayWidth.Of($"+{labels.Count}") : 0;
        var gap = used == 0 ? 0 : 1;
        var room = width - used - gap - reserve;
        var text = $" {labels[index]} ";

        if (DisplayWidth.Of(text) <= room)
        {
            return new CardLabelChip(labels[index], text);
        }

        if (used > 0 || room < MinCutChip)
        {
            return null;
        }

        return new CardLabelChip(labels[index], $" {CardText.Shorten(labels[index], room - ChipPadding)} ");
    }

    private static List<CardLabelRow> Close(List<List<CardLabelChip>> rows, int hidden, int width)
    {
        var result = new List<CardLabelRow>();
        var lastFilled = rows.FindLastIndex(static row => row.Count > 0);

        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Count == 0)
            {
                continue;
            }

            var last = index == lastFilled;
            var fits = Width(rows[index]) + 1 + DisplayWidth.Of($"+{hidden}") <= width;

            result.Add(new CardLabelRow(rows[index], hidden > 0 && last && fits ? hidden : 0));
        }

        if (result.Count == 0)
        {
            result.Add(new CardLabelRow([], hidden));
        }

        return result;
    }

    private static int Width(List<CardLabelChip> chips)
    {
        return chips.Sum(static chip => DisplayWidth.Of(chip.Text)) + Math.Max(0, chips.Count - 1);
    }
}
