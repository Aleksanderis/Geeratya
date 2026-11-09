using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Text;
using Geeratya.Core.Views;

namespace Geeratya.Tui.Rendering;

public sealed partial class ColumnsBodyRenderer
{
    /// <summary>
    /// The text of the title line of the board, which the status bar shows now: the sprint's name, dates, days left,
    /// count and points on a sprint board, else the title and count, then the view options and the loading note, and
    /// on a sprint board the goal last when <paramref name="withGoal"/> (the status bar leaves it out whole when it does
    /// not fit, it never cuts it).
    /// </summary>
    public static string StatusText(TabState tab, Glyphs glyphs, DateTimeOffset now, bool withGoal = true)
    {
        var parts = new List<string>();

        if (tab.Sprint is { } sprint && tab.Definition.Source == ViewSourceKind.Sprint)
        {
            parts.Add(sprint.Name);
            parts.Add(SprintHeaderText.Dates(sprint));
            parts.Add(now == default ? string.Empty : SprintHeaderText.DaysLeft(sprint, now));
            parts.Add(ListBodyRenderer.CountText(tab));
            parts.Add(SprintHeaderText.Points(tab.Rows));
        }
        else
        {
            parts.Add(tab.Definition.Title);
            parts.Add(ListBodyRenderer.CountText(tab));
            parts.Add(BoardViewOptions.Summary(tab, glyphs.Separator));
        }

        if (ListBodyRenderer.FooterLine(tab, glyphs) is { } note)
        {
            parts.Add(note.PlainText);
        }

        if (withGoal)
        {
            parts.Add(GoalText(tab));
        }

        return string.Join(glyphs.Separator, parts.Where(part => part.Length > 0));
    }

    /// <summary>The goal of the sprint a Sprint view shows on one line, or empty when there is none.</summary>
    public static string GoalText(TabState tab)
    {
        if (tab is not { Sprint.Goal: { } goal } || tab.Definition.Source != ViewSourceKind.Sprint)
        {
            return string.Empty;
        }

        return string.Join(' ', goal.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>A Sprint view of "the active sprint" whose lookup finished with none: say so instead of "empty".</summary>
    private static bool NoActiveSprint(TabState tab)
    {
        return tab is { Definition.Source: ViewSourceKind.Sprint, SprintResolved: true, Sprint: null, Load: IdleState }
            && SprintSource.TryParse(tab.Definition.SourceValue, out var source)
            && source.SprintId is null;
    }

    private static string NoSprintHint(KeymapTable keymap)
    {
        var palette = keymap.FindKeyLabel(InputMode.Normal, ActionIds.PaletteOpen);

        return $"Plan one in the backlog: {palette}backlog";
    }

    /// <summary>
    /// The top edge. A window of several columns shows each column's title as a chip, the selected one in the heading
    /// role, and the columns left out on each side at the right end (<c>‹2 1›</c>). A window of one column names
    /// its neighbours around the title, inside the edge.
    /// </summary>
    private static StyledLine TopEdge(
        TabState tab,
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        BoardWindow window,
        FrameGrid grid,
        List<PaneRect> panes,
        bool focused)
    {
        if (window.Count == 1 && window.Total > 1)
        {
            return NeighbourEdge(tab, columns, cursor, window, grid);
        }

        var chips = new List<EdgeChip>(window.Count * 2);
        var hidden = HiddenText(window);

        for (var position = 0; position < window.Count; position++)
        {
            var column = window.First + position;
            var title = new List<StyledSpan> { new(ColumnTitle(tab, columns[column]), TextRole.Muted) };
            var right = position == window.Count - 1 && hidden.Length > 0
                ? new List<StyledSpan> { new(hidden, TextRole.Muted) }
                : null;

            chips.AddRange(FrameGrid.Chips(panes[position], title, right, focused && column == cursor.Column));
        }

        return new StyledLine(grid.Row(0, static (_, _) => [], chips));
    }

    /// <summary>The number of columns left out on each side, as <c>‹2 1›</c>; empty when all columns show.</summary>
    private static string HiddenText(BoardWindow window)
    {
        var left = window.HiddenLeft > 0 ? $"{HiddenLeftMark}{window.HiddenLeft}" : string.Empty;
        var right = window.HiddenRight > 0 ? $"{window.HiddenRight}{HiddenRightMark}" : string.Empty;

        return string.Join(' ', new[] { left, right }.Where(part => part.Length > 0));
    }

    /// <summary>
    /// Top edge of a window of one column: the selected column between the names of its neighbours, with
    /// <c>‹</c> and <c>›</c> where more columns lie beyond. The cells follow <see cref="BoardHeaderText"/>, which the
    /// mouse hit test reads.
    /// </summary>
    private static StyledLine NeighbourEdge(
        TabState tab,
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        BoardWindow window,
        FrameGrid grid)
    {
        var selected = cursor.Column;
        var corner = $"{grid.Glyph(0, 0)}{HorizontalChar}";
        var spans = new List<StyledSpan>(5) { new(corner, TextRole.FrameFocus) };

        if (selected > 0)
        {
            var left = BoardHeaderText.LeftNeighbour(columns[selected - 1], tab.Next is not null);

            spans.Add(new StyledSpan(left, TextRole.Muted));
        }

        spans.Add(new StyledSpan(ColumnTitle(tab, columns[selected]), TextRole.ChipFocus));

        if (selected < columns.Count - 1)
        {
            var right = BoardHeaderText.RightNeighbour(columns[selected + 1], tab.Next is not null);

            spans.Add(new StyledSpan(right, TextRole.Muted));
        }

        var fitted = Fitted(spans, window.Width - 1, HorizontalChar);

        return new StyledLine([.. fitted.Spans, new StyledSpan(grid.Glyph(0, window.Width - 1), TextRole.FrameFocus)]);
    }

    /// <summary>The spans cut to <paramref name="width"/> columns, then filled with <paramref name="fill"/>.</summary>
    private static StyledLine Fitted(List<StyledSpan> spans, int width, char fill)
    {
        var fitted = new List<StyledSpan>(spans.Count + 1);
        var left = width;

        foreach (var span in spans)
        {
            var text = TextWidth.Fit(span.Text, left);

            fitted.Add(new StyledSpan(text, span.Role));
            left -= TextWidth.Of(text);
        }

        fitted.Add(new StyledSpan(new string(fill, Math.Max(0, left)), TextRole.FrameFocus));

        return new StyledLine(fitted);
    }

    private static string ColumnTitle(TabState tab, BoardColumnGroup column)
    {
        return BoardHeaderText.Title(column, tab.Next is not null);
    }
}
