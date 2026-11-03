using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Core.Adf;

internal static partial class AdfTableRenderer
{
    private static void RenderGrid(
        List<List<TableCell>> rows,
        TableCell?[][] owners,
        int[] widths,
        List<StyledLine> output)
    {
        var heights = RowHeights(rows);
        var firstLines = new int[rows.Count];

        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            firstLines[rowIndex] = firstLines[rowIndex - 1] + heights[rowIndex - 1];
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var lineIndex = 0; lineIndex < heights[rowIndex]; lineIndex++)
            {
                output.Add(RowLine(owners[rowIndex], widths, firstLines, firstLines[rowIndex] + lineIndex));
            }

            if (rowIndex == 0 && IsHeaderRow(rows[0]))
            {
                output.Add(HeaderRule(widths));
            }
        }
    }

    /// <summary>
    /// A row is as tall as its tallest cell; a cell with a row span that is taller than the rows it covers makes the
    /// last of them taller.
    /// </summary>
    private static int[] RowHeights(List<List<TableCell>> rows)
    {
        var heights = new int[rows.Count];

        Array.Fill(heights, 1);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            foreach (var cell in rows[rowIndex])
            {
                if (cell.RowSpan == 1)
                {
                    heights[rowIndex] = Math.Max(heights[rowIndex], cell.Lines.Count);
                }
            }
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            foreach (var cell in rows[rowIndex])
            {
                if (cell.RowSpan > 1)
                {
                    GrowLastRow(heights, cell);
                }
            }
        }

        return heights;
    }

    private static void GrowLastRow(int[] heights, TableCell cell)
    {
        var last = cell.Row + cell.RowSpan - 1;
        var covered = 0;

        for (var rowIndex = cell.Row; rowIndex <= last; rowIndex++)
        {
            covered += heights[rowIndex];
        }

        if (cell.Lines.Count > covered)
        {
            heights[last] += cell.Lines.Count - covered;
        }
    }

    private static bool IsHeaderRow(List<TableCell> row)
    {
        return row.Count > 0 && row.TrueForAll(static cell => cell.IsHeader && cell.RowSpan == 1);
    }

    private static StyledLine HeaderRule(int[] widths)
    {
        var text = new StringBuilder("|");

        for (var index = 0; index < widths.Length; index++)
        {
            text.Append(HeaderRuleCharacter, widths[index] + 2);
            text.Append('|');
        }

        return StyledLine.Of(text.ToString(), TextRole.Muted);
    }

    /// <summary>One output line of a row; <paramref name="gridLine" /> counts from the table's first line.</summary>
    private static StyledLine RowLine(TableCell?[] owners, int[] widths, int[] firstLines, int gridLine)
    {
        var spans = new List<StyledSpan> { new(RowStart, TextRole.Muted) };
        var column = 0;

        while (column < widths.Length)
        {
            var cell = owners[column];

            if (cell is null)
            {
                spans.Add(new StyledSpan(new string(' ', widths[column]), TextRole.Body));
                column++;
            }
            else
            {
                AppendCell(spans, cell, gridLine - firstLines[cell.Row], SpanWidth(widths, cell));
                column = cell.Column + cell.Span;
            }

            AppendSeparator(spans, column >= widths.Length);
        }

        return new StyledLine(spans);
    }

    private static void AppendCell(List<StyledSpan> spans, TableCell cell, int lineIndex, int cellWidth)
    {
        var used = 0;

        if (lineIndex < cell.Lines.Count)
        {
            var line = cell.Lines[lineIndex];

            spans.AddRange(line.Spans);
            used = LineWidth(line);
        }

        if (used < cellWidth)
        {
            spans.Add(new StyledSpan(new string(' ', cellWidth - used), TextRole.Body));
        }
    }

    private static void AppendSeparator(List<StyledSpan> spans, bool last)
    {
        spans.Add(new StyledSpan(last ? RowEnd : CellSeparator, TextRole.Muted));
    }

    private static void RenderStacked(
        List<List<TableCell>> rows,
        int columnCount,
        RenderContext context,
        List<StyledLine> output)
    {
        var hasHeader = IsHeaderRow(rows[0]) && rows.Count > 1;
        var labels = hasHeader ? HeaderLabels(rows[0], columnCount) : new string[columnCount];
        var produced = false;

        for (var rowIndex = hasHeader ? 1 : 0; rowIndex < rows.Count; rowIndex++)
        {
            var rowLines = new List<StyledLine>();

            RenderStackedRow(rows[rowIndex], labels, context, rowLines);

            if (rowLines.Count == 0)
            {
                continue;
            }

            if (produced)
            {
                output.Add(StyledLine.Empty);
            }

            output.AddRange(rowLines);
            produced = true;
        }
    }

    private static string[] HeaderLabels(List<TableCell> header, int columnCount)
    {
        var labels = new string[columnCount];

        foreach (var cell in header)
        {
            var text = string.Join(' ', cell.Lines.Select(static line => line.PlainText)).Trim();

            labels[cell.Column] = text;
        }

        return labels;
    }

    private static void RenderStackedRow(
        List<TableCell> row,
        string[] labels,
        RenderContext context,
        List<StyledLine> output)
    {
        foreach (var cell in row)
        {
            var label = labels[cell.Column];

            if (string.IsNullOrEmpty(label))
            {
                var bullet = new LinePrefix(StackedBullet, TextRole.Muted);

                AdfBlockRenderer.RenderPrefixed(cell.Content, context, true, bullet, bullet.Hanging(), output);

                continue;
            }

            var inline = new LinePrefix($"{label}{LabelSuffix}", TextRole.Muted);

            if (inline.Width <= context.Width / 2)
            {
                AdfBlockRenderer.RenderPrefixed(cell.Content, context, true, inline, inline.Hanging(), output);

                continue;
            }

            var heading = new WrappedText(context.Width, false);
            var indent = new LinePrefix(StackedIndent, TextRole.Body);

            heading.Append(label, TextRole.Muted);
            heading.Finish(output);
            AdfBlockRenderer.RenderPrefixed(cell.Content, context, true, indent, indent, output);
        }
    }

    private static int WidestLine(List<StyledLine> lines)
    {
        var widest = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            widest = Math.Max(widest, LineWidth(lines[index]));
        }

        return widest;
    }

    private static int LineWidth(StyledLine line)
    {
        var width = 0;

        for (var index = 0; index < line.Spans.Count; index++)
        {
            width += DisplayWidth.Of(line.Spans[index].Text);
        }

        return width;
    }
}
