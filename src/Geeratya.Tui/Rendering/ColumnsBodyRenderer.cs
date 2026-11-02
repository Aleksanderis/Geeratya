using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The body of the active tab in the columns layout (<c>docs/ux-screens.md</c>, "Columns layout"): the title with
/// the count, one header per board column and then only the cards in the viewport of each drawn column. Columns that
/// do not fit are left out around the selected one. Only the visible cards are drawn, never all the loaded ones.
/// </summary>
public sealed class ColumnsBodyRenderer : IBodyRenderer
{
    private const string NoCardsText = "no issues";
    private const string HiddenLeftMark = "‹";
    private const string HiddenRightMark = "›";
    private const string MoreMark = "+";
    private const string SelectedMark = ">";
    private const string Blank = " ";
    private const int FirstSummaryLine = 2;
    private const int BlankRow = int.MaxValue;
    private const int Margin = 1;
    private const int MinimumBoxWidth = 8;
    private const string Vertical = "│";
    private const char HorizontalChar = '─';
    private const string TopLeft = "┌";
    private const string TopJoin = "┬";
    private const string TopRight = "┐";
    private const string BottomLeft = "└";
    private const string BottomJoin = "┴";
    private const string BottomRight = "┘";

    public bool AppliesTo(AppState state)
    {
        var tab = ListBodyRenderer.ActiveTab(state);

        return state.Mode != InputMode.Help && tab is { Definition.Layout: ViewLayout.Columns };
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ListBodyRenderer.ActiveTab(state)!;
        var glyphs = Glyphs.For(context.Glyphs);
        var area = Math.Max(0, height - ListLayout.HeaderRows);
        var columns = ColumnsNavigation.ColumnsOf(tab);
        var cards = BoardCards.Visible(area, ListLayout.HeaderRows);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, cards);
        var window = BoardWindows.For(columns.Count, cursor.Column, width);
        var lines = new List<StyledLine>(Math.Min(height, ListLayout.HeaderRows + area))
        {
            TitleLine(tab, window, width, glyphs),
        };

        if (tab.Rows.Count == 0 || columns.Count == 0)
        {
            lines.Add(StyledLine.Empty);
            ListBodyRenderer.AddBodyMessage(lines, tab, width, glyphs);

            return lines;
        }

        lines.Add(HeaderLine(tab, columns, cursor, window));

        var compact = BoardCards.IsCompact(area, ListLayout.HeaderRows);

        var cardRows = Math.Max(0, area - BoardCards.FrameRows);

        for (var row = 0; row < cardRows; row++)
        {
            lines.Add(row < cards * (compact ? 1 : BoardCards.FullRows)
                ? CardRow(columns, cursor, window, row, compact)
                : CardRow(columns, cursor, window, BlankRow, compact));
        }

        if (area > 0)
        {
            lines.Add(BottomLine(window));
        }

        return lines;
    }

    private static StyledLine TitleLine(TabState tab, BoardWindow window, int width, Glyphs glyphs)
    {
        var spans = new List<StyledSpan>
        {
            new($"{tab.Definition.Title}{glyphs.Separator}{ListBodyRenderer.CountText(tab)}", TextRole.Heading),
        };

        if (ListBodyRenderer.FooterLine(tab, glyphs) is { } note)
        {
            spans.Add(new StyledSpan(glyphs.Separator, TextRole.Muted));
            spans.AddRange(note.Spans);
        }

        var hidden = HiddenText(window);
        var used = spans.Sum(span => TextWidth.Of(span.Text));

        if (hidden.Length > 0 && used + 1 + TextWidth.Of(hidden) <= width)
        {
            spans.Add(new StyledSpan(new string(' ', width - used - TextWidth.Of(hidden)), TextRole.Body));
            spans.Add(new StyledSpan(hidden, TextRole.Muted));
        }

        return new StyledLine(spans);
    }

    /// <summary>The number of columns left out on each side, as <c>‹2 1›</c>; empty when all columns show.</summary>
    private static string HiddenText(BoardWindow window)
    {
        var left = window.HiddenLeft > 0 ? $"{HiddenLeftMark}{window.HiddenLeft}" : string.Empty;
        var right = window.HiddenRight > 0 ? $"{window.HiddenRight}{HiddenRightMark}" : string.Empty;

        return string.Join(' ', new[] { left, right }.Where(part => part.Length > 0));
    }

    private static StyledLine HeaderLine(
        TabState tab,
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        BoardWindow window)
    {
        if (window.Count == 1 && window.Total > 1)
        {
            return NeighbourHeader(tab, columns, cursor, window);
        }

        var spans = new List<StyledSpan>(window.Count * 3);

        for (var position = 0; position < window.Count; position++)
        {
            var column = window.First + position;
            var selected = column == cursor.Column;
            var width = window.ColumnWidth(position);
            var last = position == window.Count - 1;
            var room = Math.Max(0, width - 2 - (last ? 1 : 0));
            var title = TextWidth.Fit($" {ColumnTitle(tab, columns[column])} ", room);
            var fill = width - 2 - TextWidth.Of(title) - (last ? 1 : 0);

            spans.Add(new StyledSpan(position == 0 ? TopLeft : TopJoin, TextRole.Muted));
            spans.Add(new StyledSpan(HorizontalChar.ToString(), TextRole.Muted));
            spans.Add(new StyledSpan(title, selected ? TextRole.Heading : TextRole.Muted));
            spans.Add(new StyledSpan(new string(HorizontalChar, Math.Max(0, fill)), TextRole.Muted));
        }

        spans.Add(new StyledSpan(TopRight, TextRole.Muted));

        return new StyledLine(spans);
    }

    /// <summary>
    /// Header of a window of one column: the selected column between the names of its neighbours, with
    /// <c>‹</c> and <c>›</c> where more columns lie beyond, inside the frame.
    /// </summary>
    private static StyledLine NeighbourHeader(
        TabState tab,
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        BoardWindow window)
    {
        var selected = cursor.Column;
        var spans = new List<StyledSpan>(5) { new(TopLeft + HorizontalChar, TextRole.Muted) };

        if (selected > 0)
        {
            spans.Add(new StyledSpan($"{HiddenLeftMark} {ColumnTitle(tab, columns[selected - 1])}  ", TextRole.Muted));
        }

        spans.Add(new StyledSpan(ColumnTitle(tab, columns[selected]), TextRole.Heading));

        if (selected < columns.Count - 1)
        {
            spans.Add(new StyledSpan($"  {ColumnTitle(tab, columns[selected + 1])} {HiddenRightMark}", TextRole.Muted));
        }

        var fitted = Fitted(spans, window.Width - 1, HorizontalChar);

        return new StyledLine([.. fitted.Spans, new StyledSpan(TopRight, TextRole.Muted)]);
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

        fitted.Add(new StyledSpan(new string(fill, Math.Max(0, left)), TextRole.Muted));

        return new StyledLine(fitted);
    }

    private static string ColumnTitle(TabState tab, BoardColumnGroup column)
    {
        var more = tab.Next is not null ? MoreMark : string.Empty;

        return $"{column.Name} {column.Rows.Count}{more}";
    }

    private static StyledLine BottomLine(BoardWindow window)
    {
        var spans = new List<StyledSpan>(window.Count + 1);

        for (var position = 0; position < window.Count; position++)
        {
            var last = position == window.Count - 1;
            var width = window.ColumnWidth(position) - (last ? 1 : 0);

            var corner = position == 0 ? BottomLeft : BottomJoin;

            spans.Add(new StyledSpan(corner + new string(HorizontalChar, Math.Max(0, width - 1)), TextRole.Muted));
        }

        spans.Add(new StyledSpan(BottomRight, TextRole.Muted));

        return new StyledLine(spans);
    }

    private static StyledLine CardRow(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        BoardWindow window,
        int row,
        bool compact)
    {
        var spans = new List<StyledSpan>(window.Count * 5);

        for (var position = 0; position < window.Count; position++)
        {
            var column = window.First + position;
            var last = position == window.Count - 1;
            var inner = Math.Max(0, window.ColumnWidth(position) - 1 - (last ? 1 : 0));

            spans.Add(new StyledSpan(Vertical, TextRole.Muted));
            AddCell(spans, columns[column], cursor, column, row, compact, inner);
        }

        spans.Add(new StyledSpan(Vertical, TextRole.Muted));

        return new StyledLine(spans);
    }

    private static void AddCell(
        List<StyledSpan> spans,
        BoardColumnGroup group,
        BoardCursor cursor,
        int column,
        int row,
        bool compact,
        int inner)
    {
        if (group.Rows.Count == 0)
        {
            spans.Add(row == 0
                ? new StyledSpan(TextWidth.Pad($" {NoCardsText}", inner), TextRole.Muted)
                : new StyledSpan(new string(' ', inner), TextRole.Body));

            return;
        }

        var pitch = compact ? 1 : BoardCards.FullRows;
        var card = row == BlankRow ? BlankRow : cursor.Scrolls[column] + (row / pitch);
        var within = row % pitch;

        if (card >= group.Rows.Count)
        {
            spans.Add(new StyledSpan(new string(' ', inner), TextRole.Body));

            return;
        }

        var selected = column == cursor.Column && card == cursor.Cards[column];
        var issue = group.Rows[card];

        if (compact)
        {
            var role = selected ? TextRole.Selected : TextRole.Body;

            spans.Add(new StyledSpan(TextWidth.Pad(CompactLine(issue, selected, inner), inner), role));

            return;
        }

        AddBoxRow(spans, issue, within, selected, inner);
    }

    /// <summary>One row of the bordered card, in a margin of one cell on each side of the column.</summary>
    private static void AddBoxRow(List<StyledSpan> spans, IssueSummary issue, int within, bool selected, int inner)
    {
        var width = inner - (2 * Margin);

        if (width < MinimumBoxWidth)
        {
            spans.Add(new StyledSpan(new string(' ', inner), TextRole.Body));

            return;
        }

        var frame = selected ? TextRole.Selected : TextRole.Muted;
        var text = selected ? TextRole.Selected : TextRole.Body;
        var heavy = selected;

        spans.Add(new StyledSpan(Blank, TextRole.Body));

        if (within == 0 || within == BoardCards.FullRows - 1)
        {
            var (left, right) = (within == 0, heavy) switch
            {
                (true, true) => ("┏", "┓"),
                (true, false) => ("┌", "┐"),
                (false, true) => ("┗", "┛"),
                _ => ("└", "┘"),
            };
            var bar = new string(heavy ? '━' : HorizontalChar, width - 2);

            spans.Add(new StyledSpan($"{left}{bar}{right}", frame));
        }
        else
        {
            var side = heavy ? "┃" : Vertical;

            spans.Add(new StyledSpan(side, frame));
            spans.Add(new StyledSpan(TextWidth.Pad(BoxText(issue, within, width - 4), width - 2), text));
            spans.Add(new StyledSpan(side, frame));
        }

        spans.Add(new StyledSpan(Blank, TextRole.Body));
    }

    private static string BoxText(IssueSummary issue, int within, int width)
    {
        if (within == 1)
        {
            var marker = IssueTypeMarker.For(issue.IssueType);

            return $" {KeyLine($"{marker} {issue.Key}", issue.Assignee, width)} ";
        }

        var lines = CardText.TwoLines(issue.Summary, width);

        return $" {(within == FirstSummaryLine ? lines.First : lines.Second)} ";
    }

    private static string CompactLine(IssueSummary issue, bool selected, int width)
    {
        var mark = selected ? SelectedMark : Blank;
        var marker = IssueTypeMarker.For(issue.IssueType);

        return CardText.Shorten($"{mark}{marker} {issue.Key} {issue.Summary}", width);
    }

    /// <summary>The key line with the assignee's initials at the right edge, when there is room.</summary>
    private static string KeyLine(string key, IssueAssignee? assignee, int width)
    {
        var initials = assignee is null ? string.Empty : AssigneeInitials.Of(assignee.DisplayName);

        if (initials.Length == 0 || width < TextWidth.Of(key) + TextWidth.Of(initials) + 1)
        {
            return TextWidth.Fit(key, width);
        }

        var keyWidth = width - TextWidth.Of(initials) - 1;

        return $"{TextWidth.Pad(key, keyWidth)} {initials}";
    }
}
