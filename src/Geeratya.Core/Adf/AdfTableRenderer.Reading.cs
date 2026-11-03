using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Adf;

internal static partial class AdfTableRenderer
{
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
