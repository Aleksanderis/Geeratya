using System.Globalization;
using Geeratya.Core.Backlog;
using Geeratya.Core.Flash;
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
    private const string CollapsedMark = "▸";
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
        var framed = FrameGrid.CanFrame(width, height);
        var inner = framed ? width - 2 : width;
        var area = Math.Max(0, height - ListLayout.HeaderRows - ListLayout.FooterRows);
        var layout = BacklogSections.Build(tab);
        var columns = ListViewColumns.ForBacklog(tab, inner);
        var note = ListBodyRenderer.FooterLine(tab, glyphs);
        var lines = new List<StyledLine>(Math.Min(height, ListLayout.HeaderRows + area));
        var dividers = new List<PaneDivider>();
        var focusLine = -1;

        // Too small for a frame: the title stays a plain line above the column titles.
        if (!framed)
        {
            lines.Add(ListBodyRenderer.HeaderLine(tab, note, glyphs));
        }

        lines.Add(ListBodyRenderer.TitlesLine(columns));

        var flash = FlashLabelsOf(state, tab, inner);
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

                var onCursor = index == tab.Selected && tab.FocusedSprintId is null;

                if (onCursor)
                {
                    focusLine = lines.Count;
                }

                var rowLine = ListBodyRenderer.RowLine(row, columns, onCursor, set, inner, glyphs);

                lines.Add(flash.TryGetValue(index, out var label) ? FlashOverlay.Apply(rowLine, label) : rowLine);

                continue;
            }

            if (framed && line == found.Section.FirstLine)
            {
                dividers.Add(Divider(tab, found.Section, lines.Count, glyphs));

                if (IsOnCursor(tab, found.Section))
                {
                    focusLine = lines.Count;
                }
            }

            lines.Add(NonRowLine(tab, found.Section, line, inner, glyphs));
        }

        var chips = ListBodyRenderer.ChipsOf(tab, note, glyphs);

        return PaneFrame.Build(width, height, chips.Left, chips.Right, lines, dividers, focusLine);
    }

    private static Dictionary<int, FlashLabel> FlashLabelsOf(AppState state, TabState tab, int inner)
    {
        var labels = new Dictionary<int, FlashLabel>();

        if (state.FlashJump is { Text.Length: > 0 } flash)
        {
            foreach (var label in FlashLabeler.ComputeBacklog(tab, flash.Text, inner))
            {
                labels[label.Row] = label;
            }
        }

        return labels;
    }

    /// <summary>
    /// The shared edge of a section: the name and state as the left chip, dates, count and points as the right one.
    /// A collapsed section is only this edge, marked with <c>▸</c>; the cursor on it selects the chip.
    /// </summary>
    private static PaneDivider Divider(TabState tab, BacklogSection section, int contentIndex, Glyphs glyphs)
    {
        var name = section.Collapsed ? $"{CollapsedMark} {section.Name}" : section.Name;

        if (section.State == SprintState.Active)
        {
            name = $"{name}{glyphs.Separator}{ActiveWord}";
        }

        var onCursor = IsOnCursor(tab, section);
        var role = TextRole.Strong;

        // The marker says where the cursor is without colour, in the print frame and the ASCII tier.
        name = onCursor ? $"> {name}" : name;
        var parts = new List<string>();

        if (!section.IsBacklog)
        {
            parts.Add(DatesText(section.Sprint));
        }

        var loadingMore = tab.Next is not null;
        var plus = loadingMore ? "+" : string.Empty;

        parts.Add($"{section.RowCount}{plus} {(section.RowCount == 1 && !loadingMore ? "issue" : "issues")}");

        if (section.HasEstimates)
        {
            parts.Add($"{PointsText(section.Points)}{plus} pts");
        }

        var right = new StyledSpan(string.Join(glyphs.Separator, parts), TextRole.Muted);

        return new PaneDivider(contentIndex, [new StyledSpan(name, role)], [right]);
    }

    private static bool IsOnCursor(TabState tab, BacklogSection section)
    {
        var onCursor = section.Collapsed && section.HasRows && section.FirstRow == tab.Selected
            && tab.FocusedSprintId is null;

        return onCursor || (section.Sprint is { } sprint && sprint.Id == tab.FocusedSprintId);
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

        if (IsOnCursor(tab, section))
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
