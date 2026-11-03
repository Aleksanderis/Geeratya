using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The Confirm dialog: a box centred over the body with the question (wrapped, naming what changes), the warning
/// line and two answer rows, <c>y</c> with the name of the action and <c>n</c> Cancel. Both rows are clickable and so
/// is the rest of the box (a click there does nothing); a click outside it cancels. Pure.
/// </summary>
public static class ConfirmOverlay
{
    private const int KeyGap = 2;

    /// <summary>
    /// Draws the dialog of <paramref name="request"/> over <paramref name="body"/> (exactly <paramref name="height"/>
    /// lines), or returns null when it does not fit: nothing is drawn then, so the caller keeps the question in the
    /// status line.
    /// </summary>
    public static IReadOnlyList<StyledLine>? Compose(
        IReadOnlyList<StyledLine> body,
        ConfirmRequest request,
        KeymapTable keymap,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        if (ConfirmDialog.Layout(request, width, height) is not { } layout)
        {
            return null;
        }

        var box = BoxLines(layout, keymap);
        var top = (height - box.Count) / 2;
        var left = (width - layout.BoxWidth) / 2;
        var result = body.ToList();

        regions?.Add(new OverlayRegion(MouseTarget.ConfirmBox, top, left, layout.BoxWidth, box.Count, 0));
        OverlayBox.Mark(regions, MouseTarget.ConfirmYes, top, left, layout.BoxWidth, layout.YesRow, [0]);
        OverlayBox.Mark(regions, MouseTarget.ConfirmNo, top, left, layout.BoxWidth, layout.NoRow, [0]);

        for (var index = 0; index < box.Count; index++)
        {
            result[top + index] = OverlayBox.Place(box[index], left, width);
        }

        return result;
    }

    private static List<StyledLine> BoxLines(ConfirmDialogLayout layout, KeymapTable keymap)
    {
        var contentWidth = layout.BoxWidth - 2;
        var lines = new List<StyledLine> { OverlayBox.Top(layout.BoxWidth, ConfirmDialog.Title) };

        foreach (var text in layout.Question)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Of(text, TextRole.Body), contentWidth));
        }

        if (layout.Warning.Count > 0)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth));

            foreach (var text in layout.Warning)
            {
                lines.Add(OverlayBox.Wall(StyledLine.Of(text, TextRole.RateLimited), contentWidth));
            }
        }

        var yes = keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmYes) ?? string.Empty;
        var no = keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmNo) ?? string.Empty;
        var keyWidth = Math.Max(TextWidth.Of(yes), TextWidth.Of(no));

        lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth));
        lines.Add(OverlayBox.Wall(AnswerLine(yes, layout.YesLabel, keyWidth, layout.TextColumns), contentWidth));
        lines.Add(OverlayBox.Wall(AnswerLine(no, ConfirmDialog.CancelLabel, keyWidth, layout.TextColumns), contentWidth));
        lines.Add(OverlayBox.Bottom(layout.BoxWidth));

        return lines;
    }

    private static StyledLine AnswerLine(string keys, string label, int keyWidth, int width)
    {
        var gap = new string(' ', KeyGap);
        var room = Math.Max(0, width - keyWidth - KeyGap);

        return new StyledLine(
        [
            new StyledSpan(TextWidth.Pad(keys, keyWidth), TextRole.Key),
            new StyledSpan(gap, TextRole.Body),
            new StyledSpan(TextWidth.Fit(label, room), TextRole.Strong),
        ]);
    }
}
