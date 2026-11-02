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
    private const string Separator = " · ";
    private const string NoCardsText = "no issues";
    private const string HiddenLeftMark = "‹";
    private const string HiddenRightMark = "›";
    private const string MoreMark = "+";
    private const string SelectedMark = ">";
    private const string Blank = " ";
    private const int FirstSummaryLine = 1;
    private const int SeparatorLine = 3;

    private static StyledSpan Empty => new(string.Empty, TextRole.Body);

    public bool AppliesTo(AppState state)
    {
        var tab = ListBodyRenderer.ActiveTab(state);

        return state.Mode != InputMode.Help && tab is { Definition.Layout: ViewLayout.Columns };
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ListBodyRenderer.ActiveTab(state)!;
        var area = Math.Max(0, height - ListLayout.HeaderRows);
        var columns = ColumnsNavigation.ColumnsOf(tab);
        var cards = BoardCards.Visible(area, ListLayout.HeaderRows);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, cards);
        var window = BoardWindows.For(columns.Count, cursor.Column, width);
        var lines = new List<StyledLine>(Math.Min(height, ListLayout.HeaderRows + area))
        {
            TitleLine(tab, window, width),
        };

        if (tab.Rows.Count == 0 || columns.Count == 0)
        {
            lines.Add(StyledLine.Empty);
            ListBodyRenderer.AddBodyMessage(lines, tab, width);

            return lines;
        }

        lines.Add(HeaderLine(tab, columns, cursor, window));

        var compact = BoardCards.IsCompact(area, ListLayout.HeaderRows);

        for (var row = 0; row < area; row++)
        {
            lines.Add(CardRow(columns, cursor, window, row, compact));
        }

        return lines;
    }

    private static StyledLine TitleLine(TabState tab, BoardWindow window, int width)
    {
        var spans = new List<StyledSpan>
        {
            new($"{tab.Definition.Title}{Separator}{ListBodyRenderer.CountText(tab)}", TextRole.Heading),
        };

        if (ListBodyRenderer.FooterLine(tab) is { } note)
        {
            spans.Add(new StyledSpan(Separator, TextRole.Muted));
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

        var spans = new List<StyledSpan>(window.Count);

        for (var position = 0; position < window.Count; position++)
        {
            var column = window.First + position;
            var selected = column == cursor.Column;
            var title = selected ? $"[{ColumnTitle(tab, columns[column])}]" : $" {ColumnTitle(tab, columns[column])}";
            var role = selected ? TextRole.Heading : TextRole.Muted;

            spans.Add(new StyledSpan(TextWidth.Pad(title, window.ColumnWidth(position)), role));
        }

        return new StyledLine(spans);
    }

    /// <summary>
    /// Header of a window of one column: the selected column in brackets between the names of its neighbours, with
    /// <c>‹</c> and <c>›</c> where more columns lie beyond.
    /// </summary>
    private static StyledLine NeighbourHeader(
        TabState tab,
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        BoardWindow window)
    {
        var selected = cursor.Column;
        var spans = new List<StyledSpan>(3);

        if (selected > 0)
        {
            spans.Add(new StyledSpan($"{HiddenLeftMark} {ColumnTitle(tab, columns[selected - 1])}  ", TextRole.Muted));
        }

        spans.Add(new StyledSpan($"[{ColumnTitle(tab, columns[selected])}]", TextRole.Heading));

        if (selected < columns.Count - 1)
        {
            spans.Add(new StyledSpan($"  {ColumnTitle(tab, columns[selected + 1])} {HiddenRightMark}", TextRole.Muted));
        }

        return Fitted(spans, window.Width);
    }

    /// <summary>The spans cut to <paramref name="width"/> columns and padded with spaces to exactly that.</summary>
    private static StyledLine Fitted(List<StyledSpan> spans, int width)
    {
        var fitted = new List<StyledSpan>(spans.Count + 1);
        var left = width;

        foreach (var span in spans)
        {
            var text = TextWidth.Fit(span.Text, left);

            fitted.Add(new StyledSpan(text, span.Role));
            left -= TextWidth.Of(text);
        }

        fitted.Add(new StyledSpan(new string(' ', left), TextRole.Body));

        return new StyledLine(fitted);
    }

    private static string ColumnTitle(TabState tab, BoardColumnGroup column)
    {
        var more = tab.Next is not null ? MoreMark : string.Empty;

        return $"{column.Name} {column.Rows.Count}{more}";
    }

    private static StyledLine CardRow(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        BoardWindow window,
        int row,
        bool compact)
    {
        var spans = new List<StyledSpan>(window.Count * 2);

        for (var position = 0; position < window.Count; position++)
        {
            var column = window.First + position;
            var cellWidth = window.ColumnWidth(position);
            var textWidth = Math.Max(0, cellWidth - 1);
            var cell = Cell(columns[column], cursor, column, row, compact, textWidth);

            spans.Add(new StyledSpan(TextWidth.Pad(cell.Text, textWidth), cell.Role));
            spans.Add(new StyledSpan(new string(' ', cellWidth - textWidth), TextRole.Body));
        }

        return new StyledLine(spans);
    }

    private static StyledSpan Cell(
        BoardColumnGroup group,
        BoardCursor cursor,
        int column,
        int row,
        bool compact,
        int width)
    {
        if (group.Rows.Count == 0)
        {
            return row == 0 ? new StyledSpan(NoCardsText, TextRole.Muted) : Empty;
        }

        var pitch = compact ? 1 : BoardCards.FullRows;
        var card = cursor.Scrolls[column] + (row / pitch);
        var within = row % pitch;

        if (card >= group.Rows.Count || within == SeparatorLine)
        {
            return Empty;
        }

        var selected = column == cursor.Column && card == cursor.Cards[column];
        var role = selected ? TextRole.Selected : TextRole.Body;

        return new StyledSpan(CardLine(group.Rows[card], within, compact, selected, width), role);
    }

    private static string CardLine(IssueSummary issue, int within, bool compact, bool selected, int width)
    {
        var mark = selected ? SelectedMark : Blank;
        var marker = IssueTypeMarker.For(issue.IssueType);

        if (compact)
        {
            return CardText.Shorten($"{mark}{marker} {issue.Key} {issue.Summary}", width);
        }

        if (within == 0)
        {
            return KeyLine($"{mark}{marker} {issue.Key}", issue.Assignee, width);
        }

        var lines = CardText.TwoLines(issue.Summary, width - 1);

        return within == FirstSummaryLine ? $"{Blank}{lines.First}" : $"{Blank}{lines.Second}";
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
