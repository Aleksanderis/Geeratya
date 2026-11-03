using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Selection;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The body of the active tab in the list layout (<c>docs/ux-screens.md</c>, "List layout"): a header with the
/// title and count, the column titles, then only the rows in the viewport, so the work follows the screen and not
/// the loaded rows. Rows are key, type marker, summary, status and assignee; narrow terminals drop columns.
/// </summary>
public sealed class ListBodyRenderer : IBodyRenderer
{
    private const string LoadingMoreWords = "Loading more";
    private const string LoadingWords = "Loading";
    private const string NoIssuesText = "No issues match.";
    private const string NoBoardIssuesText = "The board has no issues.";
    private const string NoOpenSprintText = "No open sprint on this board.";
    private const string NoOpenSprintHint = "Plan one in the backlog (:backlog).";
    private const string RetryText = "<space>r to retry";
    private const string TruncatedText = "Load more stopped at 10 000 rows.";
    private const string CouldNotLoadMore = "Could not load more: ";
    private const string KeyTitle = "Key";
    private const string TypeTitle = "T";
    private const string SummaryTitle = "Summary";
    private const string StatusTitle = "Status";
    private const string StatusGlyphTitle = "S";
    private const string AssigneeTitle = "Assignee";
    private const string SprintTitle = "Sprint";
    private const string PointsTitle = "Pts";
    private const string EpicTitle = "Epic";
    private const string Unassigned = "-";
    private const string UnknownGlyph = "?";

    private static readonly HashSet<string> _noMarks = [];

    public bool AppliesTo(AppState state)
    {
        return state.Mode != InputMode.Help && ActiveTab(state) is { Definition.Layout: ViewLayout.List };
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ActiveTab(state)!;
        var glyphs = Glyphs.For(context.Glyphs);
        var framed = FrameGrid.CanFrame(width, height);
        var inner = framed ? width - 2 : width;
        var area = Math.Max(0, height - ListLayout.HeaderRows - ListLayout.FooterRows);
        var columns = OptionalColumns(tab, inner, area);
        var footer = FooterLine(tab, glyphs);
        var shown = tab.Rows.Count == 0 ? 0 : Math.Min(area, tab.Rows.Count - FirstRow(tab));
        var footerFits = footer is not null && tab.Rows.Count > 0 && shown < area && LastRowShown(tab, shown);
        var note = footerFits ? null : footer;
        var lines = new List<StyledLine>(Math.Min(height, ListLayout.HeaderRows + area));

        // Too small for a frame: the title stays a plain line above the column titles, as the layout always had it.
        if (!framed)
        {
            lines.Add(HeaderLine(tab, note, glyphs));
        }

        lines.Add(TitlesLine(columns));

        if (tab.Rows.Count == 0)
        {
            AddBodyMessage(lines, tab, inner, glyphs);

            return Framed(tab, note, glyphs, width, height, lines);
        }

        var first = FirstRow(tab);
        var busy = BusyMarks.Keys(state);
        var range = RowSelection.VisualRange(tab);
        var marks = tab.Marks.Count == 0 ? _noMarks : new HashSet<string>(tab.Marks, StringComparer.OrdinalIgnoreCase);

        for (var index = first; index < first + shown; index++)
        {
            var row = tab.Rows[index];
            var set = SetMarkOf(row, index, marks, range);

            lines.Add(RowLine(BusyMarks.Mark(row, busy, glyphs), columns, index == tab.Selected, set, inner, glyphs));
        }

        if (footerFits)
        {
            lines.Add(footer!);
        }

        return Framed(tab, note, glyphs, width, height, lines);
    }

    /// <summary>The title chips of a one-pane frame: the source on the left, count, options and note on the right.</summary>
    internal static TitleChips ChipsOf(TabState tab, StyledLine? note, Glyphs glyphs)
    {
        var right = new List<StyledSpan> { new(CountText(tab), TextRole.Muted) };

        if (BoardViewOptions.Summary(tab, glyphs.Separator) is { Length: > 0 } options)
        {
            right.Add(new StyledSpan($"{glyphs.Separator}{options}", TextRole.Strong));
        }

        if (note is not null)
        {
            right.Add(new StyledSpan(glyphs.Separator, TextRole.Muted));
            right.AddRange(note.Spans);
        }

        return new TitleChips([new StyledSpan(tab.Definition.Title, TextRole.Heading)], right);
    }

    private static List<StyledLine> Framed(
        TabState tab,
        StyledLine? note,
        Glyphs glyphs,
        int width,
        int height,
        List<StyledLine> content)
    {
        var chips = ChipsOf(tab, note, glyphs);

        return PaneFrame.Build(width, height, chips.Left, chips.Right, content, []);
    }

    // Sprint, estimate and epic columns show only while a row in view has the value, so a Kanban list stays as before.
    private static ListColumns OptionalColumns(TabState tab, int width, int area)
    {
        bool sprint = false, points = false, epic = false;
        var first = FirstRow(tab);
        var end = Math.Min(tab.Rows.Count, first + area);

        for (var index = first; index < end; index++)
        {
            var row = tab.Rows[index];
            sprint |= row.CurrentSprint is not null;
            points |= row.Estimate is not null;
            epic |= row.Epic is not null;
        }

        return ListColumns.For(width, tab.KeyWidth, sprint, points, epic);
    }

    internal static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? state.Session.Tabs.FirstOrDefault(tab => tab.Id == id) : null;
    }

    private static int FirstRow(TabState tab)
    {
        return Math.Clamp(tab.Scroll, 0, Math.Max(0, tab.Rows.Count - 1));
    }

    private static bool LastRowShown(TabState tab, int shown)
    {
        return FirstRow(tab) + shown >= tab.Rows.Count;
    }

    internal static StyledLine HeaderLine(TabState tab, StyledLine? note, Glyphs glyphs)
    {
        var spans = new List<StyledSpan>
        {
            new($"{tab.Definition.Title}{glyphs.Separator}{CountText(tab)}", TextRole.Heading),
        };

        if (BoardViewOptions.Summary(tab, glyphs.Separator) is { Length: > 0 } options)
        {
            spans.Add(new StyledSpan(glyphs.Separator, TextRole.Muted));
            spans.Add(new StyledSpan(options, TextRole.Strong));
        }

        if (note is not null)
        {
            spans.Add(new StyledSpan(glyphs.Separator, TextRole.Muted));
            spans.AddRange(note.Spans);
        }

        return new StyledLine(spans);
    }

    internal static string CountText(TabState tab)
    {
        var count = tab.Rows.Count;

        if (tab.Next is not null || tab.Truncated)
        {
            // An approximate count below the loaded rows is stale: no total is better than a wrong one.
            if (tab.Total is not { } total || total < count)
            {
                return $"{count}+";
            }

            var about = tab.TotalIsApproximate ? "about " : string.Empty;

            return $"{count} of {about}{total}";
        }

        return count == 1 ? "1 issue" : $"{count} issues";
    }

    internal static StyledLine TitlesLine(ListColumns columns)
    {
        var statusTitle = columns.Status == ListColumns.FullStatus ? StatusTitle : StatusGlyphTitle;
        var text = string.Concat(
            new string(' ', ListColumns.MarkWidth),
            TextWidth.Pad(KeyTitle, columns.Key),
            " ",
            TypeTitle,
            " ",
            TextWidth.Pad(SummaryTitle, columns.Summary),
            " ",
            TextWidth.Pad(statusTitle, columns.Status),
            columns.Assignee > 0 ? $" {TextWidth.Pad(AssigneeTitle, columns.Assignee)}" : string.Empty,
            columns.Sprint > 0 ? $" {TextWidth.Pad(SprintTitle, columns.Sprint)}" : string.Empty,
            columns.Points > 0 ? $" {TextWidth.Pad(PointsTitle, columns.Points)}" : string.Empty,
            columns.Epic > 0 ? $" {TextWidth.Pad(EpicTitle, columns.Epic)}" : string.Empty);

        return StyledLine.Of(text, TextRole.Muted);
    }

    /// <summary>The note under the last row: loading, a failed page or the row cap. Null when there is none.</summary>
    internal static StyledLine? FooterLine(TabState tab, Glyphs glyphs)
    {
        if (tab.Rows.Count == 0)
        {
            return null;
        }

        return tab.Load switch
        {
            Loading { Reason: LoadReason.NextPage } => StyledLine.Of(LoadingMoreWords + glyphs.Ellipsis, TextRole.Loading),
            // Other failures keep the rows and are told by the banner above the list.
            Failed { Reason: LoadReason.NextPage } failed =>
                StyledLine.Of($"{CouldNotLoadMore}{failed.Message}", TextRole.Error),
            _ when tab.Truncated => StyledLine.Of(TruncatedText, TextRole.Muted),
            _ => null,
        };
    }

    internal static void AddBodyMessage(List<StyledLine> lines, TabState tab, int width, Glyphs glyphs)
    {
        if (tab.Load is Loading)
        {
            lines.Add(StyledLine.Of(LoadingWords + glyphs.Ellipsis, TextRole.Loading));

            return;
        }

        if (tab.Load is Failed failed)
        {
            lines.Add(StyledLine.Of(failed.Message, TextRole.Error));
            lines.Add(StyledLine.Of(RetryText, TextRole.Muted));

            return;
        }

        if (tab.Cold)
        {
            return;
        }

        // A Scrum board lists only the open sprints, so between sprints it is empty although the project has issues.
        if (tab.Definition.Source == ViewSourceKind.Board && tab.Board is { Type: BoardType.Scrum })
        {
            lines.Add(Centered(NoOpenSprintText, width));
            lines.Add(StyledLine.Of(Centered(NoOpenSprintHint, width).PlainText, TextRole.Muted));

            return;
        }

        var isBoard = tab.Definition.Source.IsBoardBased();

        lines.Add(Centered(isBoard ? NoBoardIssuesText : NoIssuesText, width));

        if (tab.Definition.Source == ViewSourceKind.Jql)
        {
            lines.Add(StyledLine.Of(tab.Definition.SourceValue, TextRole.Muted));
        }
    }

    private static StyledLine Centered(string text, int width)
    {
        var padding = Math.Max(0, (width - TextWidth.Of(text)) / 2);

        return StyledLine.Of($"{new string(' ', padding)}{text}");
    }

    internal static StyledLine RowLine(
        IssueSummary row,
        ListColumns columns,
        bool selected,
        SetMark set,
        int width,
        Glyphs glyphs)
    {
        var key = TextWidth.Pad(row.Key, columns.Key);
        var marker = IssueTypeMarker.For(row.IssueType);
        var summary = TextWidth.Pad(row.Summary, columns.Summary);
        var status = StatusText(row.Status, columns.Status, glyphs);
        var assignee = TextWidth.Pad(row.Assignee?.DisplayName ?? Unassigned, columns.Assignee);

        var extras = ExtraColumns(row, columns);

        // The first cell is the cursor, the second the set: `*` marked, `+` in the Visual range (never colour alone).
        var setGlyph = SetGlyph(set);
        var cursor = selected ? ">" : " ";
        var inRange = set.HasFlag(SetMark.InRange);
        var markRole = set.HasFlag(SetMark.Marked) ? TextRole.Strong : TextRole.Body;

        var spans = new List<StyledSpan>(8)
        {
            new($"{cursor}{setGlyph}", markRole),
            new(key, TextRole.Body),
            new(" ", TextRole.Body),
            new(marker.ToString(), IssueTypeRole.For(row.IssueType)),
            new($" {summary} ", TextRole.Body),
            new(status, StatusRole(row.Status)),
        };

        if (columns.Assignee > 0)
        {
            spans.Add(new StyledSpan($" {assignee}", TextRole.Muted));
        }

        if (extras.Length > 0)
        {
            spans.Add(new StyledSpan(extras, TextRole.Muted));
        }

        if (!selected && !inRange)
        {
            return new StyledLine(spans);
        }

        // The row keeps its own colours over a tint: the lone selection, or the stronger range tint with the
        // cursor row of a range underlined.
        var ground = inRange ? SpanGround.Range : SpanGround.Selection;

        if (inRange && selected)
        {
            ground |= SpanGround.Cursor;
        }

        var used = spans.Sum(span => TextWidth.Of(span.Text));

        if (used < width)
        {
            spans.Add(new StyledSpan(new string(' ', width - used), TextRole.Body));
        }

        return new StyledLine([.. spans.Select(span => span with { Ground = ground })]);
    }

    private static string SetGlyph(SetMark set)
    {
        if (set.HasFlag(SetMark.Marked))
        {
            return "*";
        }

        return set.HasFlag(SetMark.InRange) ? "+" : " ";
    }

    private static string ExtraColumns(IssueSummary row, ListColumns columns)
    {
        var text = string.Empty;

        if (columns.Sprint > 0)
        {
            text += $" {TextWidth.Pad(row.CurrentSprint?.Name ?? string.Empty, columns.Sprint)}";
        }

        if (columns.Points > 0)
        {
            var points = row.Estimate is { } estimate ? EstimateText.Format(estimate) : string.Empty;
            text += $" {TextWidth.PadLeft(points, columns.Points)}";
        }

        if (columns.Epic > 0)
        {
            text += $" {TextWidth.Pad(row.Epic?.Key ?? string.Empty, columns.Epic)}";
        }

        return text;
    }

    internal static SetMark SetMarkOf(IssueSummary row, int index, HashSet<string> marks, RowRange? range)
    {
        var inRange = range?.Contains(index) == true ? SetMark.InRange : SetMark.None;

        return marks.Contains(row.Key) ? inRange | SetMark.Marked : inRange;
    }

    private static string StatusText(IssueStatus status, int width, Glyphs glyphs)
    {
        return width == ListColumns.FullStatus ? TextWidth.Pad(status.Name, width) : StatusGlyph(status, glyphs);
    }

    private static string StatusGlyph(IssueStatus status, Glyphs glyphs)
    {
        return status.CategoryKey switch
        {
            StatusCategoryKeys.New => glyphs.ToDo,
            StatusCategoryKeys.Indeterminate => glyphs.InProgress,
            StatusCategoryKeys.Done => glyphs.Done,
            _ => UnknownGlyph,
        };
    }

    private static TextRole StatusRole(IssueStatus status)
    {
        return status.CategoryKey switch
        {
            StatusCategoryKeys.New => TextRole.StatusToDo,
            StatusCategoryKeys.Indeterminate => TextRole.StatusInProgress,
            StatusCategoryKeys.Done => TextRole.StatusDone,
            _ => TextRole.Body,
        };
    }

    [Flags]
    internal enum SetMark
    {
        None = 0,
        Marked = 1,
        InRange = 2,
    }
}
