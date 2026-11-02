using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The jump prompt box (<c>docs/ux-screens.md</c>, "Jump to an issue"): a centred box over the body with the key
/// prompt, the completions (typed key, keys of the tab, recent keys) and a key footer. Pure.
/// </summary>
public static class JumpOverlay
{
    private const string Title = "Jump to an issue";
    private const string HintText = "Type a key like GCM-12, or a number in the current project.";
    private const string RecentText = "recent";
    private const int MaxBoxWidth = 80;
    private const int NarrowBoxWidth = 24;
    private const int Margin = 4;
    private const int MinimumBoxHeight = 4;
    private const int FooterHeight = 2;
    private const int FooterMinimumInner = 8;
    private const int KeyColumn = 12;
    private const int TopGap = 2;

    /// <summary>Draws the prompt of <paramref name="state"/> over <paramref name="body"/> (exactly <paramref name="height"/> lines).</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        KeymapTable keymap,
        int width,
        int height)
    {
        var jump = state.Jump;
        var boxWidth = width >= NarrowBoxWidth ? Math.Min(width - Margin, MaxBoxWidth) : width;

        if (jump is null || boxWidth < 8 || height < MinimumBoxHeight)
        {
            return body;
        }

        var rows = JumpRows.For(state, jump);
        var selected = Math.Clamp(jump.Selected, 0, Math.Max(0, rows.Count - 1));
        var lines = RowLines(rows, selected, boxWidth - 4);
        var inner = Math.Min(height, lines.Count + 1 + FooterHeight + 2) - 2;
        var footer = inner >= FooterMinimumInner ? FooterHeight : 0;
        var box = BoxLines(jump, lines, selected, keymap, boxWidth, inner, footer);
        var top = Math.Min(TopGap, height - box.Count);
        var left = (width - boxWidth) / 2;
        var result = body.ToList();

        for (var index = 0; index < box.Count && top + index < result.Count; index++)
        {
            result[top + index] = OverlayBox.Place(box[index], left, width);
        }

        return result;
    }

    private static List<StyledLine> BoxLines(
        JumpState jump,
        List<StyledLine> rows,
        int selected,
        KeymapTable keymap,
        int boxWidth,
        int inner,
        int footer)
    {
        var contentWidth = boxWidth - 2;
        var area = Math.Max(0, inner - 1 - footer);
        var first = area <= 0 || selected < area ? 0 : Math.Min(selected - area + 1, Math.Max(0, rows.Count - area));
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, Title), OverlayBox.Wall(Prompt(jump), contentWidth) };

        foreach (var line in rows.Skip(first).Take(area))
        {
            lines.Add(OverlayBox.Wall(line, contentWidth));
        }

        while (lines.Count < inner + 1 - footer)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth));
        }

        if (footer > 0)
        {
            var rule = StyledLine.Of(new string(OverlayBox.Horizontal, contentWidth - 2), TextRole.Muted);

            lines.Add(OverlayBox.Wall(rule, contentWidth));
            lines.Add(OverlayBox.Wall(StyledLine.Of(FooterText(keymap), TextRole.Muted), contentWidth));
        }

        lines.Add(OverlayBox.Bottom(boxWidth));

        return lines;
    }

    private static StyledLine Prompt(JumpState jump)
    {
        return new StyledLine([new StyledSpan("> ", TextRole.Key), new StyledSpan($"{jump.Text}_", TextRole.Body)]);
    }

    private static List<StyledLine> RowLines(IReadOnlyList<JumpRow> rows, int selected, int width)
    {
        if (rows.Count == 0)
        {
            return [StyledLine.Of(HintText, TextRole.Muted)];
        }

        var lines = new List<StyledLine>();

        for (var index = 0; index < rows.Count; index++)
        {
            lines.Add(RowLine(rows[index], index == selected, width));
        }

        return lines;
    }

    private static StyledLine RowLine(JumpRow row, bool selected, int width)
    {
        var marker = selected ? ">" : " ";
        var origin = row.Origin == JumpOrigin.Recent ? RecentText : string.Empty;
        var detail = row.Title.Length > 0 ? row.Title : origin;
        var prefix = $"{marker} {TextWidth.Pad(row.Key, KeyColumn)}";

        if (selected)
        {
            return StyledLine.Of(TextWidth.Pad($"{prefix}{detail}", width), TextRole.Selected);
        }

        return new StyledLine(
        [
            new StyledSpan(marker, TextRole.Muted),
            new StyledSpan($" {TextWidth.Pad(row.Key, KeyColumn)}", TextRole.Strong),
            new StyledSpan(detail, TextRole.Muted),
        ]);
    }

    private static string FooterText(KeymapTable keymap)
    {
        var up = keymap.FindKeys(InputMode.Insert, ActionIds.PickerPrevious);
        var down = keymap.FindKeys(InputMode.Insert, ActionIds.PickerNext);
        var accept = keymap.FindKeys(InputMode.Insert, ActionIds.FormNext);
        var cancel = keymap.FindKeys(InputMode.Insert, ActionIds.Cancel);

        return $"{up}/{down} choose   {accept} open   {cancel} close";
    }
}
