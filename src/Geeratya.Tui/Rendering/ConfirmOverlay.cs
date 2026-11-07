using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The Confirm dialog: a box centred over the body with the question (wrapped, naming what changes), the warning
/// line and two answer rows, <c>y</c> with the name of the action and <c>n</c> Cancel. Both rows are clickable and so
/// is the rest of the box (a click there does nothing); a click outside it cancels. A notice (a request with a
/// title, <see cref="ConfirmRequest"/>) has the single close row and a click outside closes it. Pure.
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

        var kind = KindOf(request);
        var box = BoxLines(layout, keymap, kind);
        var top = (height - box.Count) / 2;
        var left = (width - layout.BoxWidth) / 2;
        var result = OverlayBox.Backdrop(body);

        regions?.Add(new OverlayRegion(MouseTarget.ConfirmBox, top, left, layout.BoxWidth, box.Count, 0));
        OverlayBox.Mark(regions, MouseTarget.ConfirmYes, top, left, layout.BoxWidth, layout.YesRow, [0]);

        if (!layout.Notice)
        {
            OverlayBox.Mark(regions, MouseTarget.ConfirmNo, top, left, layout.BoxWidth, layout.NoRow, [0]);
        }

        for (var index = 0; index < box.Count; index++)
        {
            result[top + index] = OverlayBox.Over(result[top + index], box[index], left, width);
        }

        return result;
    }

    /// <summary>
    /// The frame colour: a notice is plain accent, a destructive request the bad colour, any other question the warn
    /// colour (<c>docs/internal/ux-look.md</c>, "Confirm").
    /// </summary>
    public static OverlayKind KindOf(ConfirmRequest request)
    {
        if (request.Title is not null)
        {
            return OverlayKind.Normal;
        }

        return request.Destructive ? OverlayKind.Destructive : OverlayKind.Warn;
    }

    private static List<StyledLine> BoxLines(ConfirmDialogLayout layout, KeymapTable keymap, OverlayKind kind)
    {
        var contentWidth = layout.BoxWidth - 2;
        var lines = new List<StyledLine> { OverlayBox.Top(layout.BoxWidth, layout.Title, kind) };

        foreach (var text in layout.Question)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Of(text), contentWidth, kind));
        }

        if (layout.Warning.Count > 0)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth, kind));

            foreach (var text in layout.Warning)
            {
                lines.Add(OverlayBox.Wall(StyledLine.Of(text, TextRole.Warning), contentWidth, kind));
            }
        }

        var yes = keymap.FindKeyLabel(InputMode.Confirm, ActionIds.ConfirmYes) ?? string.Empty;
        var no = keymap.FindKeyLabel(InputMode.Confirm, ActionIds.ConfirmNo) ?? string.Empty;
        var keyWidth = Math.Max(TextWidth.Of(yes), TextWidth.Of(no));

        lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth, kind));
        lines.Add(OverlayBox.Wall(AnswerLine(yes, layout.YesLabel, keyWidth, layout.TextColumns), contentWidth, kind));

        if (!layout.Notice)
        {
            var cancel = AnswerLine(no, ConfirmDialog.CancelLabel, keyWidth, layout.TextColumns);

            lines.Add(OverlayBox.Wall(cancel, contentWidth, kind));
        }

        lines.Add(OverlayBox.Bottom(layout.BoxWidth, kind));

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
