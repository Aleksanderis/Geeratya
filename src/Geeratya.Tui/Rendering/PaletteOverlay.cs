using Geeratya.Core.CommandPalette;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The command palette (<c>docs/ux.md</c>, "Command palette"): a box over the body listing every action under group
/// subheaders with its keys, the highlighted row marked; the typed text is on the status line. A blank line precedes
/// each subheader but the first, and is dropped when the list does not fit. Pure.
/// </summary>
public static class PaletteOverlay
{
    private const string Title = "Commands";
    private const string EmptyText = "No action matches. Enter runs the text as a command.";
    private const string UnavailableText = " (not yet)";
    private const int MaxBoxWidth = 80;
    private const int NarrowBoxWidth = 24;
    private const int Margin = 4;
    private const int MinimumBoxHeight = 4;
    private const int KeyGap = 2;
    private const int TopGap = 2;
    private const int MaxBoxHeight = 18;

    /// <summary>Draws the palette of <paramref name="state"/> over <paramref name="body"/> (<paramref name="height"/> lines).</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        KeymapTable keymap,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        var boxWidth = width >= NarrowBoxWidth ? Math.Min(width - Margin, MaxBoxWidth) : width;

        if (state.Mode != InputMode.Command || boxWidth < 8 || height < MinimumBoxHeight)
        {
            return body;
        }

        var rows = PaletteRows.For(state, keymap);
        var count = rows.Count(row => row.IsSelectable);
        var selected = Math.Clamp(state.PaletteSelected, 0, Math.Max(0, count - 1));
        var area = Math.Min(height - 2 - TopGap, MaxBoxHeight - 2);
        var lines = ListLines(rows, selected, boxWidth - 4, gaps: true);

        if (lines.Count > area)
        {
            lines = ListLines(rows, selected, boxWidth - 4, gaps: false);
        }

        var first = FirstVisible(lines, area);
        var shown = lines.Skip(first).Take(area).ToList();
        var stable = StableRows(state, keymap, boxWidth - 4, area);

        while (shown.Count < stable)
        {
            shown.Add(new ListLine(StyledLine.Empty, -1, false));
        }

        var box = new List<StyledLine> { OverlayBox.Top(boxWidth, Title) };

        box.AddRange(shown.Select(line => OverlayBox.Wall(line.Text, boxWidth - 2)));
        box.Add(OverlayBox.Bottom(boxWidth));

        var left = (width - boxWidth) / 2;
        var result = OverlayBox.Backdrop(body);

        OverlayBox.Cover(regions, TopGap, left, boxWidth, box.Count);

        for (var index = 0; index < shown.Count; index++)
        {
            if (shown[index].Index >= 0)
            {
                OverlayBox.Mark(regions, MouseTarget.PaletteRow, TopGap, left, boxWidth, 1 + index, [shown[index].Index]);
            }
        }

        for (var index = 0; index < box.Count && TopGap + index < result.Count; index++)
        {
            result[TopGap + index] = OverlayBox.Over(result[TopGap + index], box[index], left, width);
        }

        return result;
    }

    // The rows of the unfiltered list (up to the limit): a box that keeps this height does not jump while the filter narrows.
    private static int StableRows(AppState state, KeymapTable keymap, int width, int area)
    {
        if (state.PaletteText.Length == 0)
        {
            return 0;
        }

        var all = PaletteRows.For(state with { PaletteText = string.Empty }, keymap);
        var count = ListLines(all, 0, width, gaps: true).Count;

        return Math.Min(count, area);
    }

    // The first line to draw: the highlighted row stays in view and, near the top, the first subheader shows.
    private static int FirstVisible(List<ListLine> lines, int area)
    {
        var at = lines.FindIndex(line => line.Selected);

        if (area <= 0 || at < area)
        {
            return 0;
        }

        return Math.Min(at - area + 1, Math.Max(0, lines.Count - area));
    }

    private static List<ListLine> ListLines(IReadOnlyList<PaletteRow> rows, int selected, int width, bool gaps)
    {
        var lines = new List<ListLine>();
        var index = 0;

        if (rows.Count == 0)
        {
            return [new ListLine(StyledLine.Of(EmptyText, TextRole.Muted), -1, false)];
        }

        foreach (var row in rows)
        {
            switch (row.Kind)
            {
                case PaletteRowKind.Header:
                    if (gaps && lines.Count > 0)
                    {
                        lines.Add(new ListLine(StyledLine.Empty, -1, false));
                    }

                    lines.Add(new ListLine(StyledLine.Of(row.Title, TextRole.Heading), -1, false));
                    break;
                case PaletteRowKind.Usage:
                case PaletteRowKind.Note:
                    lines.Add(new ListLine(StyledLine.Of(row.Title, TextRole.Muted), -1, false));
                    break;
                default:
                    lines.Add(new ListLine(RowLine(row, index == selected, width), index, index == selected));
                    index++;
                    break;
            }
        }

        return lines;
    }

    private static StyledLine RowLine(PaletteRow row, bool selected, int width)
    {
        var marker = selected ? ">" : " ";
        var keyText = TextWidth.Fit(row.Keys, Math.Max(0, width / 2));
        var gap = keyText.Length == 0 ? string.Empty : new string(' ', KeyGap);
        var room = Math.Max(0, width - 2 - TextWidth.Of(keyText) - gap.Length);
        var title = row.Enabled ? row.Title : $"{row.Title}{UnavailableText}";
        var text = TextWidth.Pad(TextWidth.Fit(title, room), room);

        if (selected)
        {
            return StyledLine.Of(TextWidth.Pad($"{marker} {text}{gap}{keyText}", width), TextRole.Selected);
        }

        var titleRole = row.Enabled ? TextRole.Body : TextRole.Muted;

        return new StyledLine(
        [
            new StyledSpan($"{marker} ", TextRole.Muted),
            new StyledSpan(text, titleRole),
            new StyledSpan(gap, TextRole.Body),
            new StyledSpan(keyText, TextRole.Muted),
        ]);
    }

    private sealed record ListLine(StyledLine Text, int Index, bool Selected);
}
