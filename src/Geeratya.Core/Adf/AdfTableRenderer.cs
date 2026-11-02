using System.Text;
using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Adf;

/// <summary>
/// Tables as text grids: column widths from the content, wrapped cells when the grid is wider than the terminal,
/// and one block per row when even the narrowest columns do not fit.
/// </summary>
internal static class AdfTableRenderer
{
    private const int NaturalWidth = 10_000;
    private const int MinColumnWidth = 8;
    private const int SeparatorWidth = 3;
    private const int BorderWidth = 1;
    private const string RowStart = "| ";
    private const string CellSeparator = " | ";
    private const string RowEnd = " |";
    private const char HeaderRuleCharacter = '-';
    private const string StackedBullet = "- ";
    private const string LabelSuffix = ": ";

    public static void Render(AdfTable table, RenderContext context, List<StyledLine> output)
    {
        var rows = ReadRows(table);

        if (rows.Count == 0)
        {
            return;
        }

        var columnCount = ColumnCount(rows);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            RenderCells(rows[rowIndex], NaturalWidth, context);
        }

        var widths = NaturalWidths(rows, columnCount);

        if (GridWidth(widths) > context.Width && !Shrink(widths, context.Width))
        {
            RenderStacked(rows, columnCount, context, output);

            return;
        }

        ReRenderIfNarrowed(rows, widths, context);
        RenderGrid(rows, widths, output);
    }

    private static List<List<TableCell>> ReadRows(AdfTable table)
    {
        var rows = new List<List<TableCell>>();

        for (var index = 0; index < table.Children.Count; index++)
        {
            if (table.Children[index] is not AdfTableRow row)
            {
                continue;
            }

            var cells = new List<TableCell>();
            var column = 0;

            for (var cellIndex = 0; cellIndex < row.Children.Count; cellIndex++)
            {
                var cell = ReadCell(row.Children[cellIndex], column);

                cells.Add(cell);
                column += cell.Span;
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static TableCell ReadCell(AdfNode node, int column)
    {
        return node switch
        {
            AdfTableHeader header => new TableCell(header.Children, true, Math.Max(1, header.ColumnSpan), column),
            AdfTableCell cell => new TableCell(cell.Children, false, Math.Max(1, cell.ColumnSpan), column),
            _ => new TableCell([node], false, 1, column),
        };
    }

    private static int ColumnCount(List<List<TableCell>> rows)
    {
        var count = 1;

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            if (row.Count > 0)
            {
                count = Math.Max(count, row[^1].Column + row[^1].Span);
            }
        }

        return count;
    }

    private static void RenderCells(List<TableCell> row, int width, RenderContext context)
    {
        for (var index = 0; index < row.Count; index++)
        {
            RenderCell(row[index], width, context);
        }
    }

    private static void RenderCell(TableCell cell, int width, RenderContext context)
    {
        var role = cell.IsHeader ? TextRole.Strong : context.BaseRole;
        var lines = new List<StyledLine>();

        AdfBlockRenderer.RenderBlocks(cell.Content, context.WithWidth(width).WithBase(role), true, lines);
        cell.Lines = lines;
    }

    private static int[] NaturalWidths(List<List<TableCell>> rows, int columnCount)
    {
        var widths = new int[columnCount];

        Array.Fill(widths, 1);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            foreach (var cell in rows[rowIndex])
            {
                if (cell.Span == 1)
                {
                    widths[cell.Column] = Math.Max(widths[cell.Column], WidestLine(cell.Lines));
                }
            }
        }

        WidenSpans(rows, widths);

        return widths;
    }

    private static void WidenSpans(List<List<TableCell>> rows, int[] widths)
    {
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            foreach (var cell in rows[rowIndex])
            {
                if (cell.Span == 1)
                {
                    continue;
                }

                var deficit = WidestLine(cell.Lines) - SpanWidth(widths, cell);

                if (deficit > 0)
                {
                    widths[Math.Min(cell.Column + cell.Span, widths.Length) - 1] += deficit;
                }
            }
        }
    }

    private static int SpanWidth(int[] widths, TableCell cell)
    {
        var end = Math.Min(cell.Column + cell.Span, widths.Length);
        var total = (end - cell.Column - 1) * SeparatorWidth;

        for (var column = cell.Column; column < end; column++)
        {
            total += widths[column];
        }

        return total;
    }

    private static int GridWidth(int[] widths)
    {
        var total = BorderWidth + (widths.Length * SeparatorWidth);

        for (var index = 0; index < widths.Length; index++)
        {
            total += widths[index];
        }

        return total;
    }

    /// <summary>Narrows the widest columns one by one; false when the grid still does not fit at the minimum.</summary>
    private static bool Shrink(int[] widths, int available)
    {
        var total = GridWidth(widths);

        while (total > available)
        {
            var widest = -1;

            for (var index = 0; index < widths.Length; index++)
            {
                if (widths[index] > MinColumnWidth && (widest < 0 || widths[index] > widths[widest]))
                {
                    widest = index;
                }
            }

            if (widest < 0)
            {
                return false;
            }

            widths[widest]--;
            total--;
        }

        return true;
    }

    private static void ReRenderIfNarrowed(List<List<TableCell>> rows, int[] widths, RenderContext context)
    {
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            foreach (var cell in rows[rowIndex])
            {
                if (WidestLine(cell.Lines) > SpanWidth(widths, cell))
                {
                    RenderCell(cell, SpanWidth(widths, cell), context);
                }
            }
        }
    }

    private static void RenderGrid(List<List<TableCell>> rows, int[] widths, List<StyledLine> output)
    {
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            RenderRow(rows[rowIndex], widths, output);

            if (rowIndex == 0 && IsHeaderRow(rows[0]))
            {
                output.Add(HeaderRule(widths));
            }
        }
    }

    private static bool IsHeaderRow(List<TableCell> row)
    {
        return row.Count > 0 && row.TrueForAll(static cell => cell.IsHeader);
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

    private static void RenderRow(List<TableCell> row, int[] widths, List<StyledLine> output)
    {
        var height = 1;

        foreach (var cell in row)
        {
            height = Math.Max(height, cell.Lines.Count);
        }

        for (var lineIndex = 0; lineIndex < height; lineIndex++)
        {
            output.Add(RowLine(row, widths, lineIndex));
        }
    }

    private static StyledLine RowLine(List<TableCell> row, int[] widths, int lineIndex)
    {
        var spans = new List<StyledSpan> { new(RowStart, TextRole.Muted) };
        var column = 0;

        for (var index = 0; index < row.Count; index++)
        {
            var cell = row[index];
            var cellWidth = SpanWidth(widths, cell);

            AppendCell(spans, cell, lineIndex, cellWidth);
            column = cell.Column + cell.Span;
            AppendSeparator(spans, column >= widths.Length);
        }

        for (; column < widths.Length; column++)
        {
            spans.Add(new StyledSpan(new string(' ', widths[column]), TextRole.Body));
            AppendSeparator(spans, column == widths.Length - 1);
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
        var hasHeader = IsHeaderRow(rows[0]);
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
            var text = string.IsNullOrEmpty(label) ? StackedBullet : $"{label}{LabelSuffix}";
            var first = new LinePrefix(text, TextRole.Muted);

            AdfBlockRenderer.RenderPrefixed(cell.Content, context, true, first, first.Hanging(), output);
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

    private sealed class TableCell(IReadOnlyList<AdfNode> content, bool isHeader, int span, int column)
    {
        public IReadOnlyList<AdfNode> Content { get; } = content;

        public bool IsHeader { get; } = isHeader;

        public int Span { get; } = span;

        public int Column { get; } = column;

        public List<StyledLine> Lines { get; set; } = [];
    }
}
