using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Adf;

/// <summary>
/// Tables as text grids: column widths from the content, wrapped cells when the grid is wider than the terminal,
/// and one block per row when even the narrowest columns do not fit.
/// </summary>
internal static partial class AdfTableRenderer
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
}
