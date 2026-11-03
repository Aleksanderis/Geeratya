using System.Globalization;
using Geeratya.Core.Backlog;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Selection;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The body of a Scrum backlog tab (<c>docs/ux-screens.md</c>, "Scrum backlog"): one list with a header per sprint
/// section and the backlog, then the issue rows of each, so the cursor crosses sections like any other row. Only the
/// lines in the viewport are built. A collapsed section shows its header alone, which carries the cursor.
/// </summary>
public sealed class BacklogBodyRenderer : IBodyRenderer
{
    private const string Expanded = "-";
    private const string Collapsed = "+";
    private const string ActiveWord = "active";
    private const string NoDatesText = "no dates";
    private const string DateFormat = "d MMM";
    private const string DateRangeSeparator = " - ";
    private const string GoalPrefix = "Goal: ";
    private const string EmptySprintText = "No issues in this sprint.";
    private const string EmptyBacklogText = "No issues in the backlog.";
    private const int Indent = ListColumns.MarkWidth;
    private const int GoalIndent = Indent + 2;

    private static readonly HashSet<string> _noMarks = [];

    public bool AppliesTo(AppState state)
    {
        return state.Mode != InputMode.Help
            && ListBodyRenderer.ActiveTab(state) is { Detail.Count: 0, Definition.Layout: ViewLayout.List } tab
            && BacklogSections.Applies(tab)
            && (tab.Rows.Count > 0 || (tab.Sprints.Count > 0 && tab.Load is IdleState));
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ListBodyRenderer.ActiveTab(state)!;
        var glyphs = Glyphs.For(context.Glyphs);
        var area = Math.Max(0, height - ListLayout.HeaderRows);
        var layout = BacklogSections.Build(tab);
        var columns = ColumnsFor(tab, width);
        var note = ListBodyRenderer.FooterLine(tab, glyphs);
        var lines = new List<StyledLine>(Math.Min(height, ListLayout.HeaderRows + area))
        {
            ListBodyRenderer.HeaderLine(tab, note, glyphs),
            ListBodyRenderer.TitlesLine(columns),
        };

        var busy = BusyMarks.Keys(state);
        var range = RowSelection.VisualRange(tab);
        var marks = tab.Marks.Count == 0 ? _noMarks : new HashSet<string>(tab.Marks, StringComparer.OrdinalIgnoreCase);
        var first = Math.Clamp(tab.Scroll, 0, Math.Max(0, layout.LineCount - 1));
        var last = Math.Min(layout.LineCount, first + area);

        for (var line = first; line < last; line++)
        {
            if (layout.LineAt(line) is not { } found)
            {
                break;
            }

            if (found.Row is { } index)
            {
                var row = BusyMarks.Mark(tab.Rows[index], busy, glyphs);
                var set = ListBodyRenderer.SetMarkOf(row, index, marks, range);

                lines.Add(ListBodyRenderer.RowLine(row, columns, index == tab.Selected && tab.FocusedSprintId is null, set, width, glyphs));

                continue;
            }

            lines.Add(NonRowLine(tab, found.Section, line, width, glyphs));
        }

        return lines;
    }

    /// <summary>
    /// The text of a section header: name, state, dates, count and points. Public so the layout of the header is
    /// tested without a frame.
    /// </summary>
    public static string HeaderText(BacklogSection section, bool loadingMore, Glyphs glyphs)
    {
        var marker = section.Collapsed ? Collapsed : Expanded;
        var parts = new List<string>();

        if (section.State == SprintState.Active)
        {
            parts.Add(ActiveWord);
        }

        if (!section.IsBacklog)
        {
            parts.Add(DatesText(section.Sprint));
        }

        var plus = loadingMore ? "+" : string.Empty;
        var noun = section.RowCount == 1 && !loadingMore ? "issue" : "issues";

        parts.Add($"{section.RowCount}{plus} {noun}");

        if (section.HasEstimates)
        {
            parts.Add($"{PointsText(section.Points)}{plus} pts");
        }

        return $"{marker} {section.Name}{glyphs.Separator}{string.Join(glyphs.Separator, parts)}";
    }

    /// <summary>The estimates as the board shows them: no fraction for whole numbers, else one decimal at most.</summary>
    public static string PointsText(double points)
    {
        return points.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string DatesText(Sprint? sprint)
    {
        if (sprint?.StartDate is not { } start || sprint.EndDate is not { } end)
        {
            return NoDatesText;
        }

        var from = start.ToString(DateFormat, CultureInfo.InvariantCulture);
        var to = end.ToString(DateFormat, CultureInfo.InvariantCulture);

        return $"{from}{DateRangeSeparator}{to}";
    }

    /// <summary>
    /// The optional columns of the whole backlog, not of the window, so they do not jump while scrolling. The sprint
    /// column is left out: the section header already says it.
    /// </summary>
    private static ListColumns ColumnsFor(TabState tab, int width)
    {
        var points = tab.Rows.Any(row => row.Estimate is not null);
        var epic = tab.Rows.Any(row => row.Epic is not null);

        return ListColumns.For(width, tab.KeyWidth, false, points, epic);
    }

    private static StyledLine NonRowLine(TabState tab, BacklogSection section, int line, int width, Glyphs glyphs)
    {
        if (line == section.FirstLine)
        {
            return HeaderLine(tab, section, width, glyphs);
        }

        if (section.HasGoalLine && line == section.FirstLine + 1)
        {
            var goal = $"{new string(' ', GoalIndent)}{GoalPrefix}{section.Sprint!.Goal}";

            return StyledLine.Of(TextWidth.Fit(goal, width), TextRole.Muted);
        }

        var text = section.IsBacklog ? EmptyBacklogText : EmptySprintText;

        return StyledLine.Of(TextWidth.Fit($"{new string(' ', GoalIndent)}{text}", width), TextRole.Muted);
    }

    private static StyledLine HeaderLine(TabState tab, BacklogSection section, int width, Glyphs glyphs)
    {
        var text = HeaderText(section, tab.Next is not null, glyphs);
        var onCursor = section.Collapsed && section.HasRows && section.FirstRow == tab.Selected
            && tab.FocusedSprintId is null;

        onCursor |= section.Sprint is { } sprint && sprint.Id == tab.FocusedSprintId;

        if (onCursor)
        {
            var selected = $"> {text}";

            return StyledLine.Of(TextWidth.Pad(TextWidth.Fit(selected, width), width), TextRole.Selected);
        }

        var nameEnd = text.IndexOf(glyphs.Separator, StringComparison.Ordinal);
        var name = nameEnd < 0 ? text : text[..nameEnd];
        var rest = nameEnd < 0 ? string.Empty : text[nameEnd..];
        var lead = new string(' ', Indent);
        var head = TextWidth.Fit($"{lead}{name}", width);
        var tail = TextWidth.Fit(rest, Math.Max(0, width - TextWidth.Of(head)));

        return new StyledLine([new StyledSpan(head, TextRole.Strong), new StyledSpan(tail, TextRole.Muted)]);
    }
}
