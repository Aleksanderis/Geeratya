using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// A pane of a <see cref="FrameGrid"/>: the cells of its box, borders included. Neighbouring panes overlap on exactly
/// the line they share.
/// </summary>
public readonly record struct PaneRect(int Row, int Column, int Height, int Width)
{
    /// <summary>The row of the bottom border.</summary>
    public int Bottom => Row + Height - 1;

    /// <summary>The column of the right border.</summary>
    public int Right => Column + Width - 1;
}

/// <summary>A title written into a horizontal border, starting at <see cref="Column"/>; replaces the border cells under it.</summary>
public readonly record struct EdgeChip(int Column, IReadOnlyList<StyledSpan> Spans)
{
    /// <summary>The cells the chip covers.</summary>
    public int Width => Spans.Sum(static span => TextWidth.Of(span.Text));
}

/// <summary>
/// The one frame of a screen body (<c>docs/internal/ux-look.md</c>, section 2): panes that tile the body share their edges, each
/// shared edge is drawn once with the junction glyph where it meets another line, and only the four outer corners are
/// rounded. Pure geometry plus a row composer, so the UI and <c>--print-frame</c> draw the same cells; the ASCII tier is
/// the frame builder's glyph swap afterwards.
/// </summary>
public sealed class FrameGrid
{
    /// <summary>Narrowest body that gets a frame; below it (or below <see cref="MinFramedHeight"/>) the content is drawn bare.</summary>
    public const int MinFramedWidth = 20;

    /// <summary>Shortest body that gets a frame.</summary>
    public const int MinFramedHeight = 5;

    /// <summary>Narrowest pane that still shows a chip.</summary>
    public const int MinChipPane = 8;

    private const int North = 1;
    private const int East = 2;
    private const int South = 4;
    private const int West = 8;
    private const string Ellipsis = "…";

    private readonly byte[] _lines;
    private readonly PaneRect? _focused;

    /// <param name="width">The columns of the body.</param>
    /// <param name="height">The rows of the body.</param>
    /// <param name="panes">The boxes that tile the body.</param>
    /// <param name="focused">The index of the focused pane, whose whole box takes the focus role; none by default.</param>
    public FrameGrid(int width, int height, IReadOnlyList<PaneRect> panes, int focused = -1)
    {
        _focused = focused >= 0 && focused < panes.Count ? panes[focused] : null;
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
        _lines = new byte[Width * Height];

        foreach (var pane in panes)
        {
            Add(pane);
        }
    }

    /// <summary>The columns of the body.</summary>
    public int Width { get; }

    /// <summary>The rows of the body.</summary>
    public int Height { get; }

    /// <summary>Whether a body of this size gets a frame at all.</summary>
    public static bool CanFrame(int width, int height)
    {
        return width >= MinFramedWidth && height >= MinFramedHeight;
    }

    /// <summary>Whether the cell is part of a border line.</summary>
    public bool IsBorder(int row, int column)
    {
        return InRange(row, column) && _lines[(row * Width) + column] != 0;
    }

    /// <summary>
    /// The role of a border cell: the focus role on the box of the focused pane (shared edges included, the focused pane
    /// wins), else the quiet border role.
    /// </summary>
    public TextRole BorderRole(int row, int column)
    {
        return _focused is { } pane && pane.Width >= 2 && pane.Height >= 2
            && row >= pane.Row && row <= pane.Bottom && column >= pane.Column && column <= pane.Right
            && (row == pane.Row || row == pane.Bottom || column == pane.Column || column == pane.Right)
            ? TextRole.FrameFocus
            : TextRole.Border;
    }

    /// <summary>The border glyph of a cell (Unicode; the ASCII tier swaps it later), empty for an interior cell.</summary>
    public string Glyph(int row, int column)
    {
        if (!InRange(row, column))
        {
            return string.Empty;
        }

        var mask = _lines[(row * Width) + column];
        var top = row == 0;
        var left = column == 0;
        var bottom = row == Height - 1;
        var right = column == Width - 1;

        return mask switch
        {
            East | South => top && left ? "╭" : "┌",
            South | West => top && right ? "╮" : "┐",
            North | East => bottom && left ? "╰" : "└",
            North | West => bottom && right ? "╯" : "┘",
            East | South | West => "┬",
            North | East | West => "┴",
            North | South | East => "├",
            North | South | West => "┤",
            North | East | South | West => "┼",
            East or West or (East | West) => "─",
            North or South or (North | South) => "│",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// One row as spans of exactly <see cref="Width"/> cells: border runs in the border or focus role, the interior
    /// runs between them from <paramref name="interior"/> (start column, cell count), and <paramref name="chips"/> written over the
    /// border cells they start on.
    /// </summary>
    public IReadOnlyList<StyledSpan> Row(
        int row,
        Func<int, int, IReadOnlyList<StyledSpan>> interior,
        IReadOnlyList<EdgeChip>? chips = null)
    {
        var spans = new List<StyledSpan>();
        var border = new StringBuilder();
        var borderRole = TextRole.Border;
        var column = 0;

        while (column < Width)
        {
            if (chips?.FirstOrDefault(candidate => candidate.Column == column && candidate.Spans.Count > 0) is { Spans.Count: > 0 } chip
                && IsBorder(row, column))
            {
                Flush(spans, border, borderRole);
                spans.AddRange(chip.Spans);
                column += Math.Max(1, chip.Width);

                continue;
            }

            if (IsBorder(row, column))
            {
                var role = BorderRole(row, column);

                if (role != borderRole)
                {
                    Flush(spans, border, borderRole);
                    borderRole = role;
                }

                border.Append(Glyph(row, column));
                column++;

                continue;
            }

            Flush(spans, border, borderRole);

            var start = column;

            while (column < Width && !IsBorder(row, column))
            {
                column++;
            }

            spans.AddRange(interior(start, column - start));
        }

        Flush(spans, border, borderRole);

        return spans;
    }

    /// <summary>
    /// The chips of a top edge of <paramref name="pane"/>: <paramref name="left"/> after the corner and the rule,
    /// <paramref name="right"/> ending before the last rule. Text is cut with <c>…</c> to what fits and a chip is dropped
    /// in a pane narrower than <see cref="MinChipPane"/>; the right chip yields to the left one. The left chip is the
    /// filled accent chip when <paramref name="focused"/>, else a dim chip; the right chip is always dim.
    /// </summary>
    public static IReadOnlyList<EdgeChip> Chips(
        PaneRect pane,
        IReadOnlyList<StyledSpan>? left,
        IReadOnlyList<StyledSpan>? right = null,
        bool focused = false)
    {
        var chips = new List<EdgeChip>(2);

        if (pane.Width < MinChipPane)
        {
            return chips;
        }

        var room = pane.Width - 4;
        var leftEnd = pane.Column + 1;

        if (left is { Count: > 0 })
        {
            var spans = Padded(Fit(focused ? ChipStyle.Focused(left) : ChipStyle.Dim(left), room - 2));

            if (spans.Count > 0)
            {
                chips.Add(new EdgeChip(pane.Column + 2, spans));
                leftEnd = pane.Column + 2 + spans.Sum(static span => TextWidth.Of(span.Text));
            }
        }

        if (right is { Count: > 0 })
        {
            var available = pane.Right - 1 - leftEnd - 1;
            var spans = Padded(Fit(ChipStyle.Dim(right), Math.Min(room, available) - 2));

            if (spans.Count > 0)
            {
                var width = spans.Sum(static span => TextWidth.Of(span.Text));

                chips.Add(new EdgeChip(pane.Right - 1 - width, spans));
            }
        }

        return chips;
    }

    /// <summary>The spans cut to <paramref name="room"/> cells; the last piece that was cut ends with an ellipsis.</summary>
    internal static List<StyledSpan> Fit(IReadOnlyList<StyledSpan> spans, int room)
    {
        var fitted = new List<StyledSpan>(spans.Count);

        if (room <= 0)
        {
            return fitted;
        }

        var left = room;

        foreach (var span in spans)
        {
            var width = TextWidth.Of(span.Text);

            if (width <= left)
            {
                fitted.Add(span);
                left -= width;

                continue;
            }

            var cut = left <= 1 ? TextWidth.Fit(span.Text, left) : $"{TextWidth.Fit(span.Text, left - 1)}{Ellipsis}";

            if (cut.Length > 0)
            {
                fitted.Add(span with { Text = cut });
            }

            break;
        }

        return fitted;
    }

    private static List<StyledSpan> Padded(List<StyledSpan> spans)
    {
        if (spans.Count == 0 || spans.All(static span => span.Text.Length == 0))
        {
            return [];
        }

        spans.Insert(0, new StyledSpan(" ", spans[0].Role));
        spans.Add(new StyledSpan(" ", spans[^1].Role));

        return spans;
    }

    private static void Flush(List<StyledSpan> spans, StringBuilder border, TextRole role)
    {
        if (border.Length > 0)
        {
            spans.Add(new StyledSpan(border.ToString(), role));
            border.Clear();
        }
    }

    private bool InRange(int row, int column)
    {
        return row >= 0 && row < Height && column >= 0 && column < Width;
    }

    private void Add(PaneRect pane)
    {
        if (pane.Width < 2 || pane.Height < 2)
        {
            return;
        }

        for (var column = pane.Column; column <= pane.Right; column++)
        {
            var sides = (column < pane.Right ? East : 0) | (column > pane.Column ? West : 0);

            Set(pane.Row, column, sides);
            Set(pane.Bottom, column, sides);
        }

        for (var row = pane.Row; row <= pane.Bottom; row++)
        {
            var sides = (row < pane.Bottom ? South : 0) | (row > pane.Row ? North : 0);

            Set(row, pane.Column, sides);
            Set(row, pane.Right, sides);
        }
    }

    private void Set(int row, int column, int sides)
    {
        if (InRange(row, column))
        {
            _lines[(row * Width) + column] |= (byte)sides;
        }
    }
}
