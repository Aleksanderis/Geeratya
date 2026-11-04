using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The which-key popup: while a multi-key binding is half typed in Normal mode, a box at the bottom right of the body
/// lists the keys that continue it. Keys and labels come from the keymap and <see cref="ActionCatalog"/>, never from
/// text of its own. A list taller than the box is split into columns; only when columns do not fit either are rows cut.
/// Pure. Opens 250 ms after the first pending key; switched off by <c>:set hints.whichKey off</c>.
/// </summary>
public static class WhichKeyOverlay
{
    private const int MaxBoxWidth = 96;
    private const int MinimumBoxWidth = 16;
    private const int MinimumBoxHeight = 3;
    private const int KeyColumn = 10;
    private const int ChromeColumns = 4;
    private const int ColumnGap = 3;

    /// <summary>
    /// True when the popup shows for <paramref name="state"/>: Normal mode, keys pending, delay passed, switch on.
    /// </summary>
    public static bool Applies(AppState state)
    {
        return WhichKeyDelay.IsOpen(state);
    }

    /// <summary>
    /// The text rows of the popup (<c>key  label</c>, group headers, blank rows between groups); empty when nothing
    /// continues. When the popup would not fit (<paramref name="room"/> rows) the blank rows go first, then the
    /// headers, and rows that still do not fit are replaced by one <c>… n more</c> row.
    /// </summary>
    public static IReadOnlyList<string> Rows(
        string pending,
        KeymapTable keymap,
        int room = int.MaxValue,
        CommandLevel level = CommandLevel.Everyday)
    {
        return Lines(pending, keymap, room, level).Select(line => line.PlainText).ToList();
    }

    /// <summary>
    /// The styled rows behind <see cref="Rows"/>: the key in the key role (bold warn, as in Gitunia's overlays), the
    /// label as body text, a header in the heading role and the <c>… n more</c> row muted.
    /// </summary>
    public static IReadOnlyList<StyledLine> Lines(
        string pending,
        KeymapTable keymap,
        int room = int.MaxValue,
        CommandLevel level = CommandLevel.Everyday)
    {
        var rows = WhichKeyBuilder.Build(keymap, InputMode.Normal, pending, level);

        if (rows.Count > room)
        {
            rows = WhichKeyBuilder.WithoutBlanks(rows);
        }

        if (rows.Count > room)
        {
            rows = WhichKeyBuilder.WithoutHeaders(rows);
        }

        var cut = rows.Count > room && room > 0;
        var lines = rows.Take(cut ? room - 1 : room).Select(Line).ToList();

        if (cut)
        {
            lines.Add(StyledLine.Of($"… {rows.Count - lines.Count} more", TextRole.Muted));
        }

        return lines;
    }

    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        KeymapTable keymap,
        int width,
        int height)
    {
        var room = height - 2;
        var everyRow = Lines(state.PendingKeys, keymap, int.MaxValue, state.Settings.CommandLevel);
        var columns = everyRow.Count > room ? Columns(everyRow, room, width - ChromeColumns) : null;
        var rows = columns ?? Lines(state.PendingKeys, keymap, room, state.Settings.CommandLevel);
        var wanted = rows.Count == 0 ? 0 : rows.Max(row => TextWidth.Of(row.PlainText)) + ChromeColumns;
        var cap = columns is null ? MaxBoxWidth : width;
        var boxWidth = Math.Min(width, Math.Min(cap, wanted));

        if (rows.Count == 0 || boxWidth < MinimumBoxWidth || height < MinimumBoxHeight)
        {
            return body;
        }

        var shown = rows.Take(height - 2).ToList();
        var contentWidth = boxWidth - 2;
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, KeyNotation.Display(state.PendingKeys)) };

        lines.AddRange(shown.Select(row => OverlayBox.Wall(row, contentWidth)));
        lines.Add(OverlayBox.Bottom(boxWidth));

        var result = body.ToList();
        var top = result.Count - lines.Count;

        for (var index = 0; index < lines.Count; index++)
        {
            result[top + index] = OverlayBox.Over(result[top + index], lines[index], width - boxWidth, width);
        }

        return result;
    }

    /// <summary>
    /// Lays <paramref name="rows"/> out side by side when they are taller than <paramref name="room"/> but fit in
    /// columns within <paramref name="maxContent"/> columns: blank rows go, the rows fill the columns
    /// top to bottom. The rows themselves when they fit as they are; null when even columns do not fit (the caller cuts
    /// the list with a <c>… n more</c> row).
    /// </summary>
    internal static IReadOnlyList<StyledLine>? Columns(IReadOnlyList<StyledLine> rows, int room, int maxContent)
    {
        if (rows.Count <= room)
        {
            return rows;
        }

        var packed = rows.Where(row => row.Spans.Count > 0).ToList();

        if (room < 1 || packed.Count == 0)
        {
            return null;
        }

        var count = (packed.Count + room - 1) / room;
        var height = (packed.Count + count - 1) / count;
        var widths = Enumerable.Range(0, count)
            .Select(column => packed.Skip(column * height).Take(height).Max(row => TextWidth.Of(row.PlainText)))
            .ToList();

        if (widths.Sum() + (count - 1) * ColumnGap > maxContent)
        {
            return null;
        }

        var result = new List<StyledLine>();

        for (var row = 0; row < height; row++)
        {
            var spans = new List<StyledSpan>();

            for (var column = 0; column < count; column++)
            {
                var index = column * height + row;
                var line = index < packed.Count ? packed[index] : StyledLine.Empty;
                var used = TextWidth.Of(line.PlainText);

                spans.AddRange(line.Spans);

                if (column < count - 1)
                {
                    spans.Add(new StyledSpan(new string(' ', widths[column] - used + ColumnGap), TextRole.Body));
                }
            }

            result.Add(new StyledLine(spans));
        }

        return result;
    }

    private static StyledLine Line(WhichKeyRow row)
    {
        if (row.IsHeader)
        {
            return StyledLine.Of(row.Label, TextRole.Heading);
        }

        if (row.IsBlank)
        {
            return StyledLine.Empty;
        }

        return new StyledLine(
        [
            new StyledSpan(row.Key.PadRight(KeyColumn), TextRole.Key),
            new StyledSpan(row.Label, TextRole.Body),
        ]);
    }
}
