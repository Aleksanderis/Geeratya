using System.Globalization;
using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.AnsiToSvg;

/// <summary>Turns tmux <c>capture-pane -e</c> text (SGR sequences only) into a grid of cells.</summary>
public static class AnsiScreen
{
    private const char Escape = '\u001b';
    private const int BrightOffset = 8;
    private const int ExtendedForeground = 38;
    private const int ExtendedBackground = 48;
    private const int IndexedMode = 5;
    private const int TrueColorMode = 2;
    private const int MaxCellWidth = 2;

    /// <summary>Parses the text into rows of exactly <paramref name="columns"/> cells; extra input is cut.</summary>
    public static Cell[][] Parse(string input, int columns, int rows)
    {
        var lines = input.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var screen = new Cell[rows][];
        var style = CellStyle.Plain;

        for (var row = 0; row < rows; row++)
        {
            var line = row < lines.Length ? lines[row] : string.Empty;
            screen[row] = ParseLine(line, columns, ref style);
        }

        return screen;
    }

    private static Cell[] ParseLine(string line, int columns, ref CellStyle style)
    {
        var cells = new Cell[columns];
        Array.Fill(cells, new Cell(" ", 1, CellStyle.Plain));
        var column = 0;
        var position = 0;

        while (position < line.Length && column < columns)
        {
            if (line[position] == Escape)
            {
                position = ReadEscape(line, position, ref style);

                continue;
            }

            var cluster = StringInfo.GetNextTextElement(line, position);
            position += cluster.Length;
            var width = Math.Min(DisplayWidth.Of(cluster), MaxCellWidth);

            if (width == 0 || column + width > columns)
            {
                continue;
            }

            cells[column] = new Cell(cluster, width, style);

            if (width == MaxCellWidth)
            {
                cells[column + 1] = new Cell(string.Empty, 0, style);
            }

            column += width;
        }

        FillBackground(cells, column, style);

        return cells;
    }

    private static void FillBackground(Cell[] cells, int from, CellStyle style)
    {
        // An active background colours the rest of the row, as a terminal paints it.
        if (style.Background == CellStyle.DefaultColor && !style.Reverse)
        {
            return;
        }

        for (var column = from; column < cells.Length; column++)
        {
            cells[column] = new Cell(" ", 1, style);
        }
    }

    private static int ReadEscape(string line, int start, ref CellStyle style)
    {
        var position = start + 1;

        if (position >= line.Length || line[position] != '[')
        {
            return position;
        }

        position++;
        var parameters = new StringBuilder();

        while (position < line.Length && line[position] is >= '0' and <= '?')
        {
            parameters.Append(line[position]);
            position++;
        }

        if (position >= line.Length)
        {
            return position;
        }

        if (line[position] == 'm')
        {
            style = ApplySgr(style, parameters.ToString());
        }

        return position + 1;
    }

    private static CellStyle ApplySgr(CellStyle style, string parameterText)
    {
        var codes = ParseCodes(parameterText);
        var index = 0;

        while (index < codes.Length)
        {
            var code = codes[index];
            index++;

            if (code is ExtendedForeground or ExtendedBackground)
            {
                var color = ReadExtendedColor(codes, ref index);

                if (color is { } value)
                {
                    style = WithColor(style, code == ExtendedForeground, value);
                }

                continue;
            }

            style = ApplyCode(style, code);
        }

        return style;
    }

    private static int[] ParseCodes(string parameterText)
    {
        if (parameterText.Length == 0)
        {
            return [0];
        }

        return [.. parameterText.Split([';', ':']).Select(ParseCode)];
    }

    private static CellStyle WithColor(CellStyle style, bool isForeground, int color)
    {
        if (isForeground)
        {
            return style with { Foreground = color };
        }

        return style with { Background = color };
    }

    private static int ParseCode(string text)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? code : 0;
    }

    private static int? ReadExtendedColor(int[] codes, ref int index)
    {
        if (index >= codes.Length)
        {
            return null;
        }

        var mode = codes[index];
        index++;

        if (mode == IndexedMode && index < codes.Length)
        {
            var palette = AnsiPalette.FromIndex(codes[index]);
            index++;

            return palette;
        }

        if (mode == TrueColorMode && index + 2 < codes.Length)
        {
            var rgb = AnsiPalette.FromRgb(codes[index], codes[index + 1], codes[index + 2]);
            index += 3;

            return rgb;
        }

        return null;
    }

    private static CellStyle ApplyCode(CellStyle style, int code)
    {
        return code switch
        {
            0 => CellStyle.Plain,
            1 => style with { Bold = true },
            2 => style with { Dim = true },
            3 => style with { Italic = true },
            4 => style with { Underline = true },
            7 => style with { Reverse = true },
            22 => style with { Bold = false, Dim = false },
            23 => style with { Italic = false },
            24 => style with { Underline = false },
            27 => style with { Reverse = false },
            >= 30 and <= 37 => style with { Foreground = AnsiPalette.FromIndex(code - 30) },
            39 => style with { Foreground = CellStyle.DefaultColor },
            >= 40 and <= 47 => style with { Background = AnsiPalette.FromIndex(code - 40) },
            49 => style with { Background = CellStyle.DefaultColor },
            >= 90 and <= 97 => style with { Foreground = AnsiPalette.FromIndex(code - 90 + BrightOffset) },
            >= 100 and <= 107 => style with { Background = AnsiPalette.FromIndex(code - 100 + BrightOffset) },
            _ => style
        };
    }
}
