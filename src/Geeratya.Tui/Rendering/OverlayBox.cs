using Geeratya.Core.Mouse;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// How serious the action an overlay asks about is; it picks the frame colour (<c>docs/internal/ux-look.md</c>, section 7).
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

    /// <summary>The top edge without a title: a rule between the two corners.</summary>
    public static StyledLine TopRule(int boxWidth, OverlayKind kind = OverlayKind.Normal)
    {
        var fill = new string(Horizontal, Math.Max(0, boxWidth - 2));

        return new StyledLine([new StyledSpan($"{Corner[0]}{fill}{Corner[1]}", FrameRole(kind))]);
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

    /// <summary>
    /// A divider that also closes a vertical rule drawn above it: <c>├──┴──┤</c> with the junction at box column
    /// <paramref name="junction"/>.
    /// </summary>
    public static StyledLine Divider(int boxWidth, int junction, OverlayKind kind = OverlayKind.Normal)
    {
        var rule = new string(Horizontal, Math.Max(0, boxWidth - 2)).ToCharArray();

        if (junction >= 1 && junction <= rule.Length)
        {
            rule[junction - 1] = '┴';
        }

        return StyledLine.Of($"├{new string(rule)}┤", FrameRole(kind));
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
    /// A box row whose right padding column carries <paramref name="marker"/> (a scroll marker from
    /// <see cref="ScrollMarker"/>) in the muted role.
    /// </summary>
    public static StyledLine Wall(
        StyledLine content,
        int contentWidth,
        string marker,
        OverlayKind kind = OverlayKind.Normal)
    {
        var frame = FrameRole(kind);
        var spans = new List<StyledSpan> { new(Vertical, frame), new(" ", TextRole.Body) };

        spans.AddRange(StyledLines.Fit(content, contentWidth - 2).Spans);
        spans.Add(new StyledSpan(marker, TextRole.Muted));
        spans.Add(new StyledSpan(Vertical, frame));

        return new StyledLine(spans);
    }

    /// <summary>
    /// The marker for row <paramref name="row"/> of <paramref name="area"/> visible rows out of
    /// <paramref name="total"/>, starting at <paramref name="first"/>: <c>▲</c> on the first row when rows are hidden
    /// above, <c>▼</c> on the last row when rows are hidden below, else a space (ASCII <c>^</c> and <c>v</c> follow
    /// from the glyph set).
    /// </summary>
    public static string ScrollMarker(int row, int area, int first, int total)
    {
        if (row == 0 && first > 0)
        {
            return "▲";
        }

        return row == area - 1 && first + area < total ? "▼" : " ";
    }

    /// <summary>
    /// A footer hint line as chips: each run of text separated by two or more spaces is a key (its first word, bold
    /// accent) and a label (muted), as <see cref="HintBar"/> draws them.
    /// </summary>
    public static StyledLine Hints(string text)
    {
        var spans = new List<StyledSpan>();
        var position = 0;

        while (position < text.Length)
        {
            var end = ChipEnd(text, position);
            var chip = text[position..end];
            var split = chip.IndexOf(' ', StringComparison.Ordinal);

            if (split < 0)
            {
                spans.Add(new StyledSpan(chip, TextRole.Muted));
            }
            else
            {
                spans.Add(new StyledSpan(chip[..split], TextRole.Key));
                spans.Add(new StyledSpan(chip[split..], TextRole.Muted));
            }

            position = end;

            while (position < text.Length && text[position] == ' ')
            {
                position++;
            }

            if (position < text.Length)
            {
                spans.Add(new StyledSpan(text[end..position], TextRole.Muted));
            }
        }

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
    /// The view behind the issue detail: muted like <see cref="Backdrop"/> and also inactive, so it carries no focus or
    /// selection mark (the focused frame is a plain border and the column title muted, a selected row or card is not tinted).
    /// </summary>
    public static List<StyledLine> Inactive(IReadOnlyList<StyledLine> body)
    {
        const SpanGround marks = SpanGround.Selection | SpanGround.Range | SpanGround.Cursor;

        return [.. body.Select(static line => new StyledLine([.. line.Spans.Select(static span => span with
        {
            Role = span.Role switch
            {
                TextRole.FrameFocus => TextRole.Border,
                TextRole.ChipFocus => TextRole.Muted,
                TextRole.Selected => TextRole.Body,
                _ => span.Role,
            },
            Ground = (span.Ground & ~marks) | SpanGround.Dim,
        })]))];
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

    // The end of the chip that starts at start: the first run of two or more spaces, or the end of the text.
    private static int ChipEnd(string text, int start)
    {
        var index = text.IndexOf("  ", start, StringComparison.Ordinal);

        return index < 0 ? text.Length : index;
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
