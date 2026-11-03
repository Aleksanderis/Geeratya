using Geeratya.Core.Mouse;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// How serious the action an overlay asks about is; it picks the frame colour (<c>docs/ux-look.md</c>, section 7).
/// </summary>
public enum OverlayKind
{
    /// <summary>The accent frame of every overlay.</summary>
    Normal,

    /// <summary>A confirm that changes something: the warn colour.</summary>
    Warn,

    /// <summary>A confirm for a destructive action: the bad colour.</summary>
    Destructive,
}

/// <summary>
/// The one floating box of every overlay (source picker, field form, palette, help, confirm, jump, menus): a rounded
/// accent frame with the title as a filled accent chip in the top edge, a raised surface inside, drawn over the
/// still-visible view, which is dimmed (<see cref="Backdrop"/>) and keeps showing around the box (<see cref="Over"/>).
/// </summary>
public static class OverlayBox
{
    public const char Horizontal = '─';

    private const string Corner = "╭╮╰╯";
    private const string Vertical = "│";
    private const int ChipChrome = 3;

    /// <summary>The top edge: one rule, the title as a filled chip, the rest of the rule.</summary>
    public static StyledLine Top(int boxWidth, string title, OverlayKind kind = OverlayKind.Normal)
    {
        var frame = FrameRole(kind);
        var chip = TextWidth.Fit($" {title} ", Math.Max(0, boxWidth - ChipChrome));
        var fill = Math.Max(0, boxWidth - ChipChrome - TextWidth.Of(chip));

        return new StyledLine(
        [
            new StyledSpan($"{Corner[0]}{Horizontal}", frame),
            new StyledSpan(chip, ChipRole(kind)),
            new StyledSpan($"{new string(Horizontal, fill)}{Corner[1]}", frame),
        ]);
    }

    /// <summary>
    /// The top edge with a dim count chip at the right (<paramref name="count"/>, for example <c>12/87</c>), a rule
    /// cell between it and the corner. The count is left out when the title and it do not fit.
    /// </summary>
    public static StyledLine Top(int boxWidth, string title, string count, OverlayKind kind = OverlayKind.Normal)
    {
        var frame = FrameRole(kind);
        var chip = TextWidth.Fit($" {title} ", Math.Max(0, boxWidth - ChipChrome));
        var tag = $" {count} ";
        var fill = boxWidth - ChipChrome - TextWidth.Of(chip) - TextWidth.Of(tag) - 1;

        if (count.Length == 0 || fill < 1)
        {
            return Top(boxWidth, title, kind);
        }

        return new StyledLine(
        [
            new StyledSpan($"{Corner[0]}{Horizontal}", frame),
            new StyledSpan(chip, ChipRole(kind)),
            new StyledSpan(new string(Horizontal, fill), frame),
            new StyledSpan(tag, TextRole.OverlayCount),
            new StyledSpan($"{Horizontal}{Corner[1]}", frame),
        ]);
    }

    /// <summary>A rule across the box that joins both walls (<c>├───┤</c>), under the query line and above the footer.</summary>
    public static StyledLine Divider(int boxWidth, OverlayKind kind = OverlayKind.Normal)
    {
        return StyledLine.Of($"├{new string(Horizontal, boxWidth - 2)}┤", FrameRole(kind));
    }

    public static StyledLine Bottom(int boxWidth, OverlayKind kind = OverlayKind.Normal)
    {
        return StyledLine.Of($"{Corner[2]}{new string(Horizontal, boxWidth - 2)}{Corner[3]}", FrameRole(kind));
    }

    /// <summary>A box row: the content padded to the inner width between two walls and a space.</summary>
    public static StyledLine Wall(StyledLine content, int contentWidth, OverlayKind kind = OverlayKind.Normal)
    {
        var frame = FrameRole(kind);
        var spans = new List<StyledSpan> { new(Vertical, frame), new(" ", TextRole.Body) };

        spans.AddRange(StyledLines.Fit(content, contentWidth - 2).Spans);
        spans.Add(new StyledSpan(" ", TextRole.Body));
        spans.Add(new StyledSpan(Vertical, frame));

        return new StyledLine(spans);
    }

    /// <summary>
    /// The view behind an overlay: every cell muted (the same lines, spans marked <see cref="SpanGround.Dim"/>), so the
    /// box stands out and the view stays readable. A body that is already dim stays as it is.
    /// </summary>
    public static List<StyledLine> Backdrop(IReadOnlyList<StyledLine> body)
    {
        return [.. body.Select(static line => Grounded(line, SpanGround.Dim))];
    }

    /// <summary>
    /// <paramref name="box"/> drawn over <paramref name="under"/> from column <paramref name="left"/>: the box cells on
    /// the raised surface, the cells of the row to the left and right of it as they were. Exactly
    /// <paramref name="width"/> columns; a wide character cut by the box edge becomes a space.
    /// </summary>
    public static StyledLine Over(StyledLine under, StyledLine box, int left, int width)
    {
        var boxWidth = TextWidth.Of(box.PlainText);
        var fitted = StyledLines.Fit(under, width);
        var spans = new List<StyledSpan>(StyledLines.Slice(fitted, 0, left).Spans);

        spans.AddRange(Grounded(box, SpanGround.Surface).Spans);
        spans.AddRange(StyledLines.Slice(fitted, left + boxWidth, width).Spans);

        return StyledLines.Fit(new StyledLine(spans), width);
    }

    /// <summary>
    /// Notes the whole box in <paramref name="regions"/> (when not null) as <see cref="MouseTarget.OverlayBox"/>: a click
    /// on a part of it that is no row does nothing, and a click outside every box is the backdrop and closes the
    /// overlay. Call it before marking the rows, which then sit on top of it.
    /// </summary>
    public static void Cover(List<OverlayRegion>? regions, int top, int left, int boxWidth, int rows)
    {
        regions?.Add(new OverlayRegion(MouseTarget.OverlayBox, top, left, boxWidth, rows, 0));
    }

    /// <summary>
    /// Notes the clickable rows of a box in <paramref name="regions"/> (when not null): the box row at
    /// <paramref name="boxTop"/> + <paramref name="offset"/> + n covers the walls' inner width and stands for
    /// <paramref name="indexes"/>[n] (a negative index is not clickable).
    /// </summary>
    public static void Mark(
        List<OverlayRegion>? regions,
        MouseTarget target,
        int boxTop,
        int left,
        int boxWidth,
        int offset,
        IEnumerable<int> indexes)
    {
        if (regions is null)
        {
            return;
        }

        var row = boxTop + offset;

        foreach (var index in indexes)
        {
            if (index >= 0)
            {
                regions.Add(new OverlayRegion(target, row, left + 1, Math.Max(1, boxWidth - 2), 1, index));
            }

            row++;
        }
    }

    private static StyledLine Grounded(StyledLine line, SpanGround ground)
    {
        return new StyledLine([.. line.Spans.Select(span => span with { Ground = span.Ground | ground })]);
    }

    private static TextRole FrameRole(OverlayKind kind)
    {
        return kind switch
        {
            OverlayKind.Warn => TextRole.OverlayFrameWarn,
            OverlayKind.Destructive => TextRole.OverlayFrameBad,
            _ => TextRole.OverlayFrame,
        };
    }

    private static TextRole ChipRole(OverlayKind kind)
    {
        return kind switch
        {
            OverlayKind.Warn => TextRole.OverlayChipWarn,
            OverlayKind.Destructive => TextRole.OverlayChipBad,
            _ => TextRole.OverlayChip,
        };
    }
}
