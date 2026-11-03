using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The which-key popup: while a multi-key binding is half typed in Normal mode, a box at the bottom of the body
/// lists the keys that continue it. Keys and labels come from the keymap and <see cref="ActionCatalog"/>, never from
/// text of its own. Rows beyond the box height are cut. Pure. Opens 250 ms after the first pending key; switched off by <c>:set whichkey off</c>.
/// </summary>
public static class WhichKeyOverlay
{
    private const int MaxBoxWidth = 50;
    private const int MinimumBoxWidth = 16;
    private const int MinimumBoxHeight = 3;
    private const int KeyColumn = 10;

    /// <summary>True when the popup shows for <paramref name="state"/>: Normal mode, keys pending, delay passed, switch on.</summary>
    public static bool Applies(AppState state)
    {
        return WhichKeyDelay.IsOpen(state);
    }

    /// <summary>
    /// The text rows of the popup (<c>key  label</c>, blank rows between clusters); empty when nothing continues.
    /// The blank rows are dropped when the popup would not fit with them (<paramref name="room"/> rows).
    /// </summary>
    public static IReadOnlyList<string> Rows(string pending, KeymapTable keymap, int room = int.MaxValue)
    {
        var rows = WhichKeyBuilder.Build(keymap, InputMode.Normal, pending);

        if (rows.Count > room)
        {
            rows = WhichKeyBuilder.WithoutBlanks(rows);
        }

        return rows.Select(row => row.IsBlank ? string.Empty : $"{row.Key.PadRight(KeyColumn)}{row.Label}").ToList();
    }

    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        KeymapTable keymap,
        int width,
        int height)
    {
        var rows = Rows(state.PendingKeys, keymap, height - 2);
        var boxWidth = Math.Min(width, MaxBoxWidth);

        if (rows.Count == 0 || boxWidth < MinimumBoxWidth || height < MinimumBoxHeight)
        {
            return body;
        }

        var shown = rows.Take(height - 2).ToList();
        var contentWidth = boxWidth - 2;
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, state.PendingKeys) };

        lines.AddRange(shown.Select(row => OverlayBox.Wall(StyledLine.Of(row), contentWidth)));
        lines.Add(OverlayBox.Bottom(boxWidth));

        var result = body.ToList();
        var top = result.Count - lines.Count;

        for (var index = 0; index < lines.Count; index++)
        {
            result[top + index] = OverlayBox.Place(lines[index], 0, width);
        }

        return result;
    }
}
