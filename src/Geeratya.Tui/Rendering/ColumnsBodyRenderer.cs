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
    private const int MinTypeName = 4;
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
    private const int NestIndent = 2;
    private const string ParentMark = "↑";
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

            var nested = group.IsNested(card);
            var line = TextWidth.Pad(CompactLine(issue, selected, nested, inner), inner);
            var at = nested ? 1 + NestIndent : 1;

            if (selected || line.Length < at + 1)
            {
                spans.Add(new StyledSpan(line, role));
            }
            else
            {
                spans.Add(new StyledSpan(line[..at], role));
                spans.Add(new StyledSpan(line.Substring(at, 1), IssueTypeRole.For(issue.IssueType)));
                spans.Add(new StyledSpan(line[(at + 1)..], role));
            }

            return;
        }

        AddBoxRow(spans, issue, within, selected, group.IsNested(card), inner);
    }

    /// <summary>One row of the bordered card, in a margin of one cell on each side of the column.</summary>
    private static void AddBoxRow(
        List<StyledSpan> spans,
        IssueSummary issue,
        int within,
        bool selected,
        bool nested,
        int inner)
    {
        var indent = nested && inner - (2 * Margin) - NestIndent >= MinimumBoxWidth ? NestIndent : 0;
        var width = inner - (2 * Margin) - indent;

        if (width < MinimumBoxWidth)
        {
            spans.Add(new StyledSpan(new string(' ', inner), TextRole.Body));

            return;
        }

        var frame = selected ? TextRole.Selected : TextRole.Muted;
        var text = selected ? TextRole.Selected : TextRole.Body;
        var heavy = selected;

        spans.Add(new StyledSpan(new string(' ', Margin + indent), TextRole.Body));

        if (within == 0 || within == BoardCards.FullRows - 1)
        {
            var (left, right) = (within == 0, heavy) switch
            {
                (true, true) => ("┏", "┓"),
                (true, false) => ("┌", "┐"),
                (false, true) => ("┗", "┛"),
                _ => ("└", "┘"),
            };
            var fill = heavy ? '━' : HorizontalChar;
            var label = within == 0 && indent == 0 && !nested ? ParentLabel(issue, width - 2, fill) : string.Empty;
            var bar = label + new string(fill, width - 2 - TextWidth.Of(label));

            spans.Add(new StyledSpan($"{left}{bar}{right}", frame));
        }
        else
        {
            var side = heavy ? "┃" : Vertical;

            spans.Add(new StyledSpan(side, frame));
            AddBoxText(spans, issue, within, selected, width - 2, text);
            spans.Add(new StyledSpan(side, frame));
        }

        spans.Add(new StyledSpan(Blank, TextRole.Body));
    }

    /// <summary>The text between the side borders; the type name on the key line is coloured by type.</summary>
    private static void AddBoxText(
        List<StyledSpan> spans,
        IssueSummary issue,
        int within,
        bool selected,
        int room,
        TextRole text)
    {
        if (within != 1)
        {
            var lines = CardText.TwoLines(issue.Summary, room - 2);

            spans.Add(new StyledSpan(
                TextWidth.Pad($" {(within == FirstSummaryLine ? lines.First : lines.Second)} ", room),
                text));

            return;
        }

        var (label, remainder) = KeyLine(issue, room - 2);
        var tail = TextWidth.Pad($"{remainder} ", room - 1 - TextWidth.Of(label));

        spans.Add(new StyledSpan(Blank, text));
        spans.Add(new StyledSpan(label, selected ? text : IssueTypeRole.For(issue.IssueType)));
        spans.Add(new StyledSpan(tail, text));
    }

    /// <summary>
    /// The small header in the top border of a card whose parent is not shown above it in the same column:
    /// <c> ↑ GTM-9 Parent summary </c>, cut to the border; empty for an issue without a parent.
    /// </summary>
    private static string ParentLabel(IssueSummary issue, int room, char fill)
    {
        if (issue.Parent is not { } parent || room < 6)
        {
            return string.Empty;
        }

        var summary = string.IsNullOrWhiteSpace(parent.Summary) ? string.Empty : $" {parent.Summary}";
        var text = CardText.Shorten($"{fill}{Blank}{ParentMark} {parent.Key}{summary}", room - 2);

        return $"{text}{Blank}";
    }

    private static string CompactLine(IssueSummary issue, bool selected, bool nested, int width)
    {
        var mark = selected ? SelectedMark : Blank;
        var marker = IssueTypeMarker.For(issue.IssueType);
        var indent = nested ? new string(' ', NestIndent) : string.Empty;
        var parent = !nested && issue.Parent is { } owner ? $" {ParentMark}{owner.Key}" : string.Empty;

        return CardText.Shorten($"{mark}{indent}{marker} {issue.Key}{parent} {issue.Summary}", width);
    }

    /// <summary>
    /// The key line: type name and key, with the assignee's initials at the right edge when there is room. The type
    /// name wins over the initials: it is cut down to <see cref="MinTypeName"/> cells, then the initials go, then the
    /// name is replaced by the one-cell marker.
    /// </summary>
    private static (string Label, string Remainder) KeyLine(IssueSummary issue, int width)
    {
        var initials = issue.Assignee is null ? string.Empty : AssigneeInitials.Of(issue.Assignee.DisplayName);
        var keyRoom = width - TextWidth.Of(issue.Key) - 1;

        if (initials.Length > 0 && keyRoom - TextWidth.Of(initials) - 1 < MinTypeName)
        {
            initials = string.Empty;
        }

        var nameRoom = initials.Length > 0 ? keyRoom - TextWidth.Of(initials) - 1 : keyRoom;
        var label = nameRoom >= MinTypeName && !string.IsNullOrWhiteSpace(issue.IssueType.Name)
            ? CardText.Shorten(issue.IssueType.Name, nameRoom)
            : IssueTypeMarker.For(issue.IssueType).ToString();
        var key = $" {issue.Key}";

        if (initials.Length == 0)
        {
            return (label, TextWidth.Fit(key, Math.Max(0, width - TextWidth.Of(label))));
        }

        return (label, $"{TextWidth.Pad(key, width - TextWidth.Of(initials) - 1 - TextWidth.Of(label))} {initials}");
    }
}
