using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The right-click menu: a small centred box over the body with one row per action of the row or line that was
/// clicked (title, then its key), the highlighted one marked. A click on a row or Enter runs it. Pure.
/// </summary>
public static class ContextMenuOverlay
{
    private const string Title = "Actions";
    private const int MaxBoxWidth = 60;
    private const int NarrowBoxWidth = 24;
    private const int Margin = 4;
    private const int MinimumBoxHeight = 4;
    private const int KeyGap = 2;
    private const int TopGap = 2;
    private const int MinimumItemsForHeaders = 6;

    /// <summary>
    /// Draws the menu of <paramref name="state"/> over <paramref name="body"/> (exactly <paramref name="height"/> lines).
    /// </summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        KeymapTable keymap,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        var boxWidth = width >= NarrowBoxWidth ? Math.Min(width - Margin, MaxBoxWidth) : width;

        if (state.ContextMenu is not { } menu || boxWidth < 8 || height < MinimumBoxHeight)
        {
            return body;
        }

        var selected = Math.Clamp(menu.Selected, 0, menu.Items.Count - 1);
        var rows = DisplayRows(menu, keymap, selected, boxWidth - 4);
        var selectedRow = rows.FindIndex(row => row.Item == selected);
        var area = Math.Min(rows.Count, height - 2);
        var first = area <= 0 || selectedRow < area ? 0 : Math.Min(selectedRow - area + 1, rows.Count - area);
        var contentWidth = boxWidth - 2;
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, Title) };

        lines.AddRange(rows.Skip(first).Take(area).Select(row => OverlayBox.Wall(row.Line, contentWidth)));

        lines.Add(OverlayBox.Bottom(boxWidth));

        var top = Math.Min(TopGap, height - lines.Count);
        var left = (width - boxWidth) / 2;
        var result = OverlayBox.Backdrop(body);

        OverlayBox.Cover(regions, top, left, boxWidth, lines.Count);

        OverlayBox.Mark(regions, MouseTarget.ContextMenu, top, left, boxWidth, 1, rows.Skip(first).Take(area).Select(row => row.Item));

        for (var index = 0; index < lines.Count && top + index < result.Count; index++)
        {
            result[top + index] = OverlayBox.Over(result[top + index], lines[index], left, width);
        }

        return result;
    }

    /// <summary>
    /// The rows of the box: a header row (not selectable, item -1) wherever the action prefix changes, when the menu
    /// has more than one prefix and enough items to need them, then the item rows.
    /// </summary>
    private static List<MenuRow> DisplayRows(ContextMenuState menu, KeymapTable keymap, int selected, int width)
    {
        var prefixes = menu.Items.Select(item => WhichKeyBuilder.PrefixOf(item.Action)).ToList();
        var headed = menu.Items.Count >= MinimumItemsForHeaders && prefixes.Distinct().Count() > 1;
        var rows = new List<MenuRow>();

        for (var index = 0; index < menu.Items.Count; index++)
        {
            var item = menu.Items[index];

            if (headed && (index == 0 || prefixes[index] != prefixes[index - 1]))
            {
                var header = StyledLine.Of($"  {WhichKeyBuilder.GroupTitle(prefixes[index])}", TextRole.Heading);

                rows.Add(new MenuRow(-1, header));
            }

            var keys = keymap.FindKeyLabel(InputMode.Normal, item.Action) ?? string.Empty;

            rows.Add(new MenuRow(index, RowLine(item.Title, keys, index == selected, width)));
        }

        return rows;
    }

    private static StyledLine RowLine(string title, string keys, bool selected, int width)
    {
        var marker = selected ? ">" : " ";
        var keyText = TextWidth.Fit(keys, Math.Max(0, width / 2));
        var gap = keyText.Length == 0 ? string.Empty : new string(' ', KeyGap);
        var room = Math.Max(0, width - 2 - TextWidth.Of(keyText) - gap.Length);
        var text = TextWidth.Pad(TextWidth.Fit(title, room), room);

        if (selected)
        {
            return StyledLine.Of(TextWidth.Pad($"{marker} {text}{gap}{keyText}", width), TextRole.Selected);
        }

        return new StyledLine(
        [
            new StyledSpan($"{marker} ", TextRole.Muted),
            new StyledSpan(text, TextRole.Body),
            new StyledSpan(gap, TextRole.Body),
            new StyledSpan(keyText, TextRole.Key),
        ]);
    }

    private sealed record MenuRow(int Item, StyledLine Line);
}
