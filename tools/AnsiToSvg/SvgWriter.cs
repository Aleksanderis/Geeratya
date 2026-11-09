using System.Globalization;
using System.Security;
using System.Text;

namespace Geeratya.AnsiToSvg;

/// <summary>Draws a cell grid as an SVG: one background rectangle per run, one text element per cell.</summary>
public static class SvgWriter
{
    public const int CellWidth = 10;
    public const int CellHeight = 20;

    private const int BaselineOffset = 15;
    private const int FontSize = 16;
    private const double DimOpacity = 0.6;
    private const string FontFamily = "DejaVu Sans Mono, Menlo, Consolas, monospace";

    public static string Write(Cell[][] screen, SvgTheme theme)
    {
        var rows = screen.Length;
        var columns = rows == 0 ? 0 : screen[0].Length;
        var builder = new StringBuilder();
        var width = columns * CellWidth;
        var height = rows * CellHeight;
        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" ");
        builder.Append(CultureInfo.InvariantCulture, $"width=\"{width}\" height=\"{height}\">\n");
        builder.Append(CultureInfo.InvariantCulture, $"<rect width=\"100%\" height=\"100%\" ");
        builder.Append(CultureInfo.InvariantCulture, $"fill=\"{Hex(theme.Background)}\"/>\n");
        builder.Append(CultureInfo.InvariantCulture, $"<g font-family=\"{FontFamily}\" font-size=\"{FontSize}\" ");
        builder.Append("xml:space=\"preserve\">\n");

        for (var row = 0; row < rows; row++)
        {
            WriteBackgrounds(builder, screen[row], row, theme);
            WriteText(builder, screen[row], row, theme);
        }

        builder.Append("</g>\n</svg>\n");

        return builder.ToString();
    }

    private static void WriteBackgrounds(StringBuilder builder, Cell[] cells, int row, SvgTheme theme)
    {
        var column = 0;

        while (column < cells.Length)
        {
            var color = Colors(cells[column].Style, theme).Background;
            var end = column + 1;

            while (end < cells.Length && Colors(cells[end].Style, theme).Background == color)
            {
                end++;
            }

            if (color != theme.Background)
            {
                var x = column * CellWidth;
                var width = (end - column) * CellWidth;
                builder.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x}\" y=\"{row * CellHeight}\" ");
                builder.Append(CultureInfo.InvariantCulture, $"width=\"{width}\" height=\"{CellHeight}\" ");
                builder.Append(CultureInfo.InvariantCulture, $"fill=\"{Hex(color)}\"/>\n");
            }

            column = end;
        }
    }

    private static void WriteText(StringBuilder builder, Cell[] cells, int row, SvgTheme theme)
    {
        for (var column = 0; column < cells.Length; column++)
        {
            var cell = cells[column];

            if (cell.Text is " " or "")
            {
                continue;
            }

            var color = Colors(cell.Style, theme).Foreground;
            var x = column * CellWidth;
            var y = (row * CellHeight) + BaselineOffset;
            builder.Append(CultureInfo.InvariantCulture, $"<text x=\"{x}\" y=\"{y}\" fill=\"{Hex(color)}\"");
            builder.Append(Attributes(cell.Style));
            builder.Append(CultureInfo.InvariantCulture, $">{SecurityElement.Escape(cell.Text)}</text>\n");
        }
    }

    private static string Attributes(CellStyle style)
    {
        var builder = new StringBuilder();

        if (style.Bold)
        {
            builder.Append(" font-weight=\"bold\"");
        }

        if (style.Italic)
        {
            builder.Append(" font-style=\"italic\"");
        }

        if (style.Underline || style.Strike)
        {
            builder.Append(CultureInfo.InvariantCulture, $" text-decoration=\"{Decoration(style)}\"");
        }

        if (style.Dim)
        {
            builder.Append(CultureInfo.InvariantCulture, $" opacity=\"{DimOpacity}\"");
        }

        return builder.ToString();
    }

    private static CellColors Colors(CellStyle style, SvgTheme theme)
    {
        var foreground = style.Foreground == CellStyle.DefaultColor ? theme.Foreground : style.Foreground;
        var background = style.Background == CellStyle.DefaultColor ? theme.Background : style.Background;

        return style.Reverse ? new CellColors(background, foreground) : new CellColors(foreground, background);
    }

    private static string Hex(int color)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{color:x6}");
    }

    private static string Decoration(CellStyle style)
    {
        if (style.Underline && style.Strike)
        {
            return "underline line-through";
        }

        return style.Underline ? "underline" : "line-through";
    }
}
