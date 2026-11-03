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
    private const string NoSprintText = "No active sprint on this board.";
    private const string HiddenLeftMark = "‹";
    private const string HiddenRightMark = "›";
    private const string SelectedMark = ">";
    private const string MarkedMark = "*";
    private const string Blank = " ";
    private const int FirstSummaryLine = 2;
    private const int BlankRow = int.MaxValue;
    private const int Margin = 1;
    private const int ChildIndent = 2;
    private const int MinimumBoxWidth = 8;
    private const string ParentMark = "↑";
    private const string Vertical = "│";
    private const char HorizontalChar = '─';

    public bool AppliesTo(AppState state)
    {
        var tab = ListBodyRenderer.ActiveTab(state);

        return state.Mode != InputMode.Help && tab is { Definition.Layout: ViewLayout.Columns };
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ListBodyRenderer.ActiveTab(state)!;
        var glyphs = Glyphs.For(context.Glyphs);
        var area = Math.Max(0, height - ListLayout.HeaderRows - ListLayout.FooterRows);
        var columns = BusyMarks.Mark(ColumnsNavigation.ColumnsOf(tab), BusyMarks.Keys(state), glyphs);
        var cards = BoardCards.Visible(area, ListLayout.HeaderRows);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, cards);
        var window = BoardWindows.For(columns.Count, cursor.Column, width);

        if (tab.Rows.Count == 0 || columns.Count == 0)
        {
            return Message(tab, context, width, height, glyphs);
        }

        if (!FrameGrid.CanFrame(width, height))
        {
            return Bare(tab, columns, cursor, width, height);
        }

        var compact = BoardCards.IsCompact(area, ListLayout.HeaderRows);
        var cardRows = BoardCards.CardRows(area, ListLayout.HeaderRows);
        var marks = tab.Marks.Count == 0 ? [] : new HashSet<string>(tab.Marks, StringComparer.OrdinalIgnoreCase);
        var panes = Panes(window, width, height);
        var grid = new FrameGrid(width, height, panes);
        var lines = new List<StyledLine>(height) { TopEdge(tab, columns, cursor, window, grid, panes) };

        for (var row = 0; row < cardRows; row++)
        {
            var card = row < cards * (compact ? 1 : BoardCards.FullRows) ? row : BlankRow;
            var spans = grid.Row(
                row + BoardCards.TopRows,
                (start, length) => Cell(columns, cursor, window, panes, start, length, card, compact, marks));

            lines.Add(new StyledLine(spans));
        }

        lines.Add(new StyledLine(grid.Row(height - 1, static (_, _) => [])));

        return lines;
    }

    /// <summary>The empty and loading states: the message lines inside the frame, the board title as the chip.</summary>
    private static List<StyledLine> Message(TabState tab, BodyContext context, int width, int height, Glyphs glyphs)
    {
        var inner = FrameGrid.CanFrame(width, height) ? width - 2 : width;
        var lines = new List<StyledLine>();

        if (NoActiveSprint(tab))
        {
            lines.Add(StyledLine.Of(NoSprintText, TextRole.Muted));
            lines.Add(StyledLine.Of(NoSprintHint(context.Keymap), TextRole.Muted));
        }
        else
        {
            ListBodyRenderer.AddBodyMessage(lines, tab, inner, glyphs);
        }

        var title = new List<StyledSpan> { new(tab.Definition.Title, TextRole.Heading) };

        return PaneFrame.Build(width, height, title, null, lines, []);
    }

    /// <summary>Below the size that gets a frame: the selected column as one-line cards under its title.</summary>
    private static List<StyledLine> Bare(
        TabState tab,
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        int width,
        int height)
    {
        var group = columns[cursor.Column];
        var lines = new List<StyledLine> { StyledLine.Of(ColumnTitle(tab, group), TextRole.Heading) };
        var first = cursor.Scrolls[cursor.Column];

        for (var card = first; card < group.Rows.Count && lines.Count < height; card++)
        {
            var selected = card == cursor.Cards[cursor.Column];
            var role = selected ? TextRole.Selected : TextRole.Body;

            lines.Add(StyledLine.Of(CompactLine(group.Rows[card], selected, false, false, false, width), role));
        }

        return lines;
    }

    /// <summary>The panes of the window: one box per drawn column, sharing the edge with the next.</summary>
    private static List<PaneRect> Panes(BoardWindow window, int width, int height)
    {
        var panes = new List<PaneRect>(window.Count);
        var column = 0;

        for (var position = 0; position < window.Count; position++)
        {
            var last = position == window.Count - 1;
            var right = last ? width - 1 : column + window.ColumnWidth(position);

            panes.Add(new PaneRect(0, column, height, right - column + 1));
            column = right;
        }

        return panes;
    }

    /// <summary>
    /// The text of the title line of the board, which the status bar shows now: the sprint's name, dates, days left,
    /// count and points on a sprint board, else the title and count, then the view options and the loading note, and
    /// on a sprint board the goal last (the status bar cuts from the end).
    /// </summary>
    public static string StatusText(TabState tab, Glyphs glyphs, DateTimeOffset now)
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

        if (tab is { Sprint.Goal: { } goal } && tab.Definition.Source == ViewSourceKind.Sprint)
        {
            parts.Add(string.Join(' ', goal.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
        }

        return string.Join(glyphs.Separator, parts.Where(part => part.Length > 0));
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
        var palette = keymap.FindKeys(InputMode.Normal, ActionIds.PaletteOpen);

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
        List<PaneRect> panes)
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
            var role = column == cursor.Column ? TextRole.Heading : TextRole.Muted;
            var title = new List<StyledSpan> { new(ColumnTitle(tab, columns[column]), role) };
            var right = position == window.Count - 1 && hidden.Length > 0
                ? new List<StyledSpan> { new(hidden, TextRole.Muted) }
                : null;

            chips.AddRange(FrameGrid.Chips(panes[position], title, right));
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
        var spans = new List<StyledSpan>(5) { new(corner, TextRole.Muted) };

        if (selected > 0)
        {
            var left = BoardHeaderText.LeftNeighbour(columns[selected - 1], tab.Next is not null);

            spans.Add(new StyledSpan(left, TextRole.Muted));
        }

        spans.Add(new StyledSpan(ColumnTitle(tab, columns[selected]), TextRole.Heading));

        if (selected < columns.Count - 1)
        {
            var right = BoardHeaderText.RightNeighbour(columns[selected + 1], tab.Next is not null);

            spans.Add(new StyledSpan(right, TextRole.Muted));
        }

        var fitted = Fitted(spans, window.Width - 1, HorizontalChar);

        return new StyledLine([.. fitted.Spans, new StyledSpan(grid.Glyph(0, window.Width - 1), TextRole.Muted)]);
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
        return BoardHeaderText.Title(column, tab.Next is not null);
    }

    /// <summary>The cells between two borders: the column whose pane holds the run starting at <paramref name="start"/>.</summary>
    private static List<StyledSpan> Cell(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        BoardWindow window,
        List<PaneRect> panes,
        int start,
        int length,
        int row,
        bool compact,
        HashSet<string> marks)
    {
        var position = 0;

        while (position + 1 < panes.Count && panes[position + 1].Column < start)
        {
            position++;
        }

        var column = window.First + position;
        var spans = new List<StyledSpan>();

        AddCell(spans, columns[column], cursor, column, row, compact, length, marks);

        return spans;
    }

    private static void AddCell(
        List<StyledSpan> spans,
        BoardColumnGroup group,
        BoardCursor cursor,
        int column,
        int row,
        bool compact,
        int inner,
        HashSet<string> marks)
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
        var marked = marks.Contains(issue.Key);

        if (compact)
        {
            var role = selected ? TextRole.Selected : TextRole.Body;

            var nested = group.IsNested(card);
            var more = group.IsNested(card + 1);
            var line = TextWidth.Pad(CompactLine(issue, selected, marked, nested, more, inner), inner);
            var at = nested ? 2 : 1;

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

        var part = group.IsNested(card)
            ? (group.IsNested(card + 1) ? GroupPart.Middle : GroupPart.Last)
            : (group.IsNested(card + 1) ? GroupPart.First : GroupPart.None);

        var nextSelected = column == cursor.Column && card + 1 == cursor.Cards[column] && group.IsNested(card + 1);

        AddBoxRow(spans, issue, within, selected, marked, part, inner, selected || nextSelected);
    }

    /// <summary>Where a card sits in the shared frame of a parent and its children in one column.</summary>
    private enum GroupPart
    {
        /// <summary>A card of its own, with its own box.</summary>
        None,

        /// <summary>The parent: it opens the shared frame.</summary>
        First,

        /// <summary>A child with more children after it in the frame.</summary>
        Middle,

        /// <summary>The last child: it closes the shared frame.</summary>
        Last,
    }

    /// <summary>One row of the bordered card, in a margin of one cell on each side of the column.</summary>
    private static void AddBoxRow(
        List<StyledSpan> spans,
        IssueSummary issue,
        int within,
        bool selected,
        bool marked,
        GroupPart part,
        int inner,
        bool heavyBottom)
    {
        var width = inner - (2 * Margin);

        if (width < MinimumBoxWidth)
        {
            spans.Add(new StyledSpan(new string(' ', inner), TextRole.Body));

            return;
        }

        var frame = selected ? TextRole.Selected : TextRole.Muted;
        var text = selected ? TextRole.Selected : TextRole.Body;

        spans.Add(new StyledSpan(new string(' ', Margin), TextRole.Body));

        if (part != GroupPart.None)
        {
            AddGroupedRow(spans, issue, within, selected, marked, part, width, heavyBottom);
        }
        else if (within == 0 || within == BoardCards.FullRows - 1)
        {
            var heavy = selected;
            var (left, right) = (within == 0, heavy) switch
            {
                (true, true) => ("┏", "┓"),
                (true, false) => ("┌", "┐"),
                (false, true) => ("┗", "┛"),
                _ => ("└", "┘"),
            };
            var fill = heavy ? '━' : HorizontalChar;
            var label = within == 0 ? ParentLabel(issue, width - 2, fill) : string.Empty;
            var bar = label + new string(fill, width - 2 - TextWidth.Of(label));

            spans.Add(new StyledSpan($"{left}{bar}{right}", frame));
        }
        else
        {
            var side = selected ? "┃" : Vertical;

            spans.Add(new StyledSpan(side, frame));
            AddBoxText(spans, issue, within, selected, marked, width - 2, text);
            spans.Add(new StyledSpan(side, frame));
        }

        spans.Add(new StyledSpan(Blank, TextRole.Body));
    }

    /// <summary>
    /// One row of a card inside the shared frame of a parent and its children. The parent is the top of the frame
    /// (top border, three text rows, divider); a child is three text rows, a blank row and a divider, the last
    /// child's divider being the bottom of the frame. The rows keep the five-row pitch of a card. A selected card
    /// has heavy sides and the <c>Selected</c> role on its text.
    /// </summary>
    private static void AddGroupedRow(
        List<StyledSpan> spans,
        IssueSummary issue,
        int within,
        bool selected,
        bool marked,
        GroupPart part,
        int width,
        bool heavyBottom)
    {
        var first = part == GroupPart.First;
        var textRows = first ? within is >= 1 and <= 3 : within <= 2;
        var blank = !first && within == 3;

        if (!textRows && !blank)
        {
            var top = first && within == 0;
            var heavy = top ? selected : heavyBottom;
            var (left, right) = (top, part == GroupPart.Last) switch
            {
                (true, _) => heavy ? ("┏", "┓") : ("┌", "┐"),
                (_, true) => heavy ? ("┗", "┛") : ("└", "┘"),
                _ => heavy ? ("┣", "┫") : ("├", "┤"),
            };
            var fill = heavy ? '━' : HorizontalChar;

            var label = first && within == 0 ? ParentLabel(issue, width - 2, fill) : string.Empty;
            var bar = label + new string(fill, width - 2 - TextWidth.Of(label));

            spans.Add(new StyledSpan($"{left}{bar}{right}", heavy ? TextRole.Selected : TextRole.Muted));

            return;
        }

        var side = selected ? "┃" : Vertical;
        var frame = selected ? TextRole.Selected : TextRole.Muted;
        var text = selected ? TextRole.Selected : first ? TextRole.Strong : TextRole.Body;

        spans.Add(new StyledSpan(side, frame));

        if (blank)
        {
            spans.Add(new StyledSpan(new string(' ', width - 2), text));
        }
        else
        {
            var indent = first ? 0 : ChildIndent;

            if (indent > 0)
            {
                spans.Add(new StyledSpan(new string(' ', indent), text));
            }

            AddBoxText(spans, issue, first ? within : within + 1, selected, marked, width - 2 - indent, text);
        }

        spans.Add(new StyledSpan(side, frame));
    }

    /// <summary>The text between the side borders; the type name on the key line is coloured by type.</summary>
    private static void AddBoxText(
        List<StyledSpan> spans,
        IssueSummary issue,
        int within,
        bool selected,
        bool marked,
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

        // The cell before the type name is the set mark: `*` for a marked card (never colour alone).
        var star = marked ? MarkedMark : Blank;
        var starRole = marked && !selected ? TextRole.Strong : text;

        spans.Add(new StyledSpan(star, starRole));
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

    private static string CompactLine(IssueSummary issue, bool selected, bool marked, bool nested, bool more, int width)
    {
        var mark = selected ? SelectedMark : Blank;
        var marker = IssueTypeMarker.For(issue.IssueType);
        var branch = nested ? (more ? "├" : "└") : string.Empty;
        var parent = !nested && issue.Parent is { } owner ? $" {ParentMark}{owner.Key}" : string.Empty;

        var star = marked ? MarkedMark : Blank;

        return CardText.Shorten($"{mark}{branch}{marker}{star}{issue.Key}{parent} {issue.Summary}", width);
    }

    /// <summary>
    /// The key line: type name and key, with the assignee's initials at the right edge when there is room. The type
    /// name wins over the initials: it is cut down to <see cref="MinTypeName"/> cells, then the initials go, then the
    /// name is replaced by the one-cell marker.
    /// </summary>
    private static (string Label, string Remainder) KeyLine(IssueSummary issue, int width)
    {
        var initials = issue.Assignee is null ? string.Empty : AssigneeInitials.Of(issue.Assignee.DisplayName);

        if (issue.Estimate is not null)
        {
            initials = $"{SprintHeaderText.Estimate(issue.Estimate)}p {initials}".TrimEnd();
        }

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
