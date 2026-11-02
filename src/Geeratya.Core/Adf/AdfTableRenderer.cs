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
    private const string StackedIndent = "  ";
    private const string LabelSuffix = ": ";

    public static void Render(AdfTable table, RenderContext context, List<StyledLine> output)
    {
        var rows = ReadRows(table);

        if (rows.Count == 0)
        {
            return;
        }

        var columnCount = ColumnCount(rows);
        var owners = Owners(rows, columnCount);

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
        RenderGrid(rows, owners, widths, output);
    }

    private static List<List<TableCell>> ReadRows(AdfTable table)
    {
        var rows = new List<List<TableCell>>();
        var rowCount = table.Children.Count(static node => node is AdfTableRow);
        var occupied = new List<int>();

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
                column = NextFreeColumn(occupied, column);

                var cell = ReadCell(row.Children[cellIndex], rows.Count, column, rowCount, occupied);

                cells.Add(cell);
                Occupy(occupied, cell);
                column += cell.Span;
            }

            rows.Add(cells);
            ReleaseRow(occupied);
        }

        return rows;
    }

    private static int NextFreeColumn(List<int> occupied, int column)
    {
        while (column < occupied.Count && occupied[column] > 0)
        {
            column++;
        }

        return column;
    }

    /// <summary>Marks the columns of a cell with a row span as taken for the rows below it.</summary>
    private static void Occupy(List<int> occupied, TableCell cell)
    {
        if (cell.RowSpan <= 1)
        {
            return;
        }

        while (occupied.Count < cell.Column + cell.Span)
        {
            occupied.Add(0);
        }

        for (var column = cell.Column; column < cell.Column + cell.Span; column++)
        {
            occupied[column] = cell.RowSpan;
        }
    }

    /// <summary>After a row, every taken column has one row less to go; the cell itself counted this row.</summary>
    private static void ReleaseRow(List<int> occupied)
    {
        for (var column = 0; column < occupied.Count; column++)
        {
            if (occupied[column] > 0)
            {
                occupied[column]--;
            }
        }
    }

    private static TableCell ReadCell(AdfNode node, int row, int column, int rowCount, List<int> occupied)
    {
        IReadOnlyList<AdfNode> content = [node];
        var isHeader = false;
        var columnSpan = 1;
        var rowSpan = 1;

        if (node is AdfTableHeader header)
        {
            content = header.Children;
            isHeader = true;
            columnSpan = header.ColumnSpan;
            rowSpan = header.RowSpan;
        }
        else if (node is AdfTableCell cell)
        {
            content = cell.Children;
            columnSpan = cell.ColumnSpan;
            rowSpan = cell.RowSpan;
        }

        var span = FreeRun(occupied, column, Math.Max(1, columnSpan));

        return new TableCell(content, isHeader, span, column, row, Math.Clamp(rowSpan, 1, rowCount - row));
    }

    /// <summary>A column span stops at the first column that a row span from above already holds.</summary>
    private static int FreeRun(List<int> occupied, int column, int wanted)
    {
        var run = 1;

        while (run < wanted && (column + run >= occupied.Count || occupied[column + run] == 0))
        {
            run++;
        }

        return run;
    }

    private static int ColumnCount(List<List<TableCell>> rows)
    {
        var count = 1;

        for (var index = 0; index < rows.Count; index++)
        {
            foreach (var cell in rows[index])
            {
                count = Math.Max(count, cell.Column + cell.Span);
            }
        }

        return count;
    }

    /// <summary>For every row and column the cell that covers it, or null for a gap.</summary>
    private static TableCell?[][] Owners(List<List<TableCell>> rows, int columnCount)
    {
        var owners = new TableCell?[rows.Count][];

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            owners[rowIndex] = new TableCell?[columnCount];
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            foreach (var cell in rows[rowIndex])
            {
                for (var covered = rowIndex; covered < rowIndex + cell.RowSpan; covered++)
                {
                    Array.Fill(owners[covered], cell, cell.Column, cell.Span);
                }
            }
        }

        return owners;
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

    private sealed class TableCell(
        IReadOnlyList<AdfNode> content,
        bool isHeader,
        int span,
        int column,
        int row,
        int rowSpan)
    {
        public IReadOnlyList<AdfNode> Content { get; } = content;

        public bool IsHeader { get; } = isHeader;

        public int Span { get; } = span;

        public int Column { get; } = column;

        public int Row { get; } = row;

        public int RowSpan { get; } = rowSpan;

        public List<StyledLine> Lines { get; set; } = [];
    }
}
