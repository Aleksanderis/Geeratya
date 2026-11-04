using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The which-key popup: while a multi-key binding is half typed in Normal mode, a box at the bottom right of the body
/// lists the keys that continue it. Keys and labels come from the keymap and <see cref="ActionCatalog"/>, never from
/// text of its own. Rows beyond the box height are cut. Pure. Opens 250 ms after the first pending key; switched off by
/// <c>:set hints.whichKey off</c>.
/// </summary>
public static class WhichKeyOverlay
{
    private const int MaxBoxWidth = 96;
    private const int MinimumBoxWidth = 16;
    private const int MinimumBoxHeight = 3;
    private const int KeyColumn = 10;
    private const int ChromeColumns = 4;

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
        var rows = Lines(state.PendingKeys, keymap, height - 2, state.Settings.CommandLevel);
        var wanted = rows.Count == 0 ? 0 : rows.Max(row => TextWidth.Of(row.PlainText)) + ChromeColumns;
        var boxWidth = Math.Min(width, Math.Min(MaxBoxWidth, wanted));

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
