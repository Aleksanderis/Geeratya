using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
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
    private const string RetryText = "<space>r to retry";
    private const string TruncatedText = "Load more stopped at 10 000 rows.";
    private const string CouldNotLoadMore = "Could not load more: ";
    private const string KeyTitle = "Key";
    private const string TypeTitle = "T";
    private const string SummaryTitle = "Summary";
    private const string StatusTitle = "Status";
    private const string StatusGlyphTitle = "S";
    private const string AssigneeTitle = "Assignee";
    private const string Unassigned = "-";
    private const string UnknownGlyph = "?";

    public bool AppliesTo(AppState state)
    {
        return state.Mode != InputMode.Help && ActiveTab(state) is { Definition.Layout: ViewLayout.List };
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ActiveTab(state)!;
        var glyphs = Glyphs.For(context.Glyphs);
        var area = Math.Max(0, height - ListLayout.HeaderRows);
        var columns = ListColumns.For(width, tab.KeyWidth);
        var footer = FooterLine(tab, glyphs);
        var shown = tab.Rows.Count == 0 ? 0 : Math.Min(area, tab.Rows.Count - FirstRow(tab));
        var footerFits = footer is not null && tab.Rows.Count > 0 && shown < area && LastRowShown(tab, shown);
        var lines = new List<StyledLine>(Math.Min(height, ListLayout.HeaderRows + area))
        {
            HeaderLine(tab, footerFits ? null : footer, glyphs),
            TitlesLine(columns),
        };

        if (tab.Rows.Count == 0)
        {
            AddBodyMessage(lines, tab, width, glyphs);

            return lines;
        }

        var first = FirstRow(tab);

        for (var index = first; index < first + shown; index++)
        {
            lines.Add(RowLine(tab.Rows[index], columns, index == tab.Selected, width, glyphs));
        }

        if (footerFits)
        {
            lines.Add(footer!);
        }

        return lines;
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

    private static StyledLine HeaderLine(TabState tab, StyledLine? note, Glyphs glyphs)
    {
        var spans = new List<StyledSpan>
        {
            new($"{tab.Definition.Title}{glyphs.Separator}{CountText(tab)}", TextRole.Heading),
        };

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
            return tab.Total is { } total
                ? $"{count} of {(tab.TotalIsApproximate ? "about " : string.Empty)}{total}"
                : $"{count}+";
        }

        return count == 1 ? "1 issue" : $"{count} issues";
    }

    private static StyledLine TitlesLine(ListColumns columns)
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
            columns.Assignee > 0 ? $" {AssigneeTitle}" : string.Empty);

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

        var isBoard = tab.Definition.Source is ViewSourceKind.Board or ViewSourceKind.Backlog;

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

    private static StyledLine RowLine(IssueSummary row, ListColumns columns, bool selected, int width, Glyphs glyphs)
    {
        var key = TextWidth.Pad(row.Key, columns.Key);
        var marker = IssueTypeMarker.For(row.IssueType);
        var summary = TextWidth.Pad(row.Summary, columns.Summary);
        var status = StatusText(row.Status, columns.Status, glyphs);
        var assignee = TextWidth.Pad(row.Assignee?.DisplayName ?? Unassigned, columns.Assignee);

        if (selected)
        {
            var assigneePart = columns.Assignee > 0 ? $" {assignee}" : string.Empty;
            var text = $"> {key} {marker} {summary} {status}{assigneePart}";

            return StyledLine.Of(TextWidth.Pad(text, width), TextRole.Selected);
        }

        var spans = new List<StyledSpan>(8)
        {
            new($"  {key} {marker} {summary} ", TextRole.Body),
            new(status, StatusRole(row.Status)),
        };

        if (columns.Assignee > 0)
        {
            spans.Add(new StyledSpan($" {assignee}", TextRole.Muted));
        }

        return new StyledLine(spans);
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
}
