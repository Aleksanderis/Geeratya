using Geeratya.Core.Lists;
using Geeratya.Core.Views;

namespace Geeratya.Core.Backlog;

/// <summary>
/// Cursor and scroll of a Scrum backlog tab. The cursor is always a row (<c>Rows[Selected]</c> keeps working for every
/// feature); <c>Scroll</c> is the first visible display line, not row. Pure: every function takes the tab and returns
/// it, clamped, so an empty backlog or a zero viewport never throws.
/// </summary>
public static class BacklogCursor
{
    /// <summary>
    /// Puts the cursor on <paramref name="row"/> (clamped; the first row of its section when that is collapsed) and
    /// scrolls so it shows, with its section header when it is the first row of the section.
    /// </summary>
    public static TabState Place(TabState tab, int row)
    {
        if (tab.Rows.Count == 0)
        {
            return tab with { Selected = 0, SelectedKey = null, Scroll = 0, FocusedSprintId = null };
        }

        var layout = BacklogSections.Build(tab);

        return Place(tab, layout, Math.Clamp(row, 0, tab.Rows.Count - 1), tab.VisibleRows);
    }

    /// <summary>
    /// Keeps the cursor where it is but makes it valid for the layout and the viewport (after a resize or a reload).
    /// </summary>
    public static TabState Settle(TabState tab, int viewport)
    {
        var layout = BacklogSections.Build(tab);

        if (tab.FocusedSprintId is { } focused && layout.EmptySprint(focused) is { } section)
        {
            return Focus(tab with { VisibleRows = viewport }, layout, section, viewport);
        }

        if (tab.Rows.Count == 0)
        {
            return tab with
            {
                Selected = 0,
                SelectedKey = null,
                Scroll = 0,
                VisibleRows = viewport,
                FocusedSprintId = null,
            };
        }

        var row = Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1);

        return Place(tab with { VisibleRows = viewport }, layout, row, viewport);
    }

    /// <summary>A motion of the keyboard across the stops of all sections.</summary>
    public static TabState Move(TabState tab, ListMove move)
    {
        var layout = BacklogSections.Build(tab);
        var count = layout.ItemCount;

        if (count == 0)
        {
            return tab;
        }

        if (tab.Rows.Count == 0 && tab.FocusedSprintId is null && layout.ItemAt(0).SprintId is { } first
            && layout.EmptySprint(first) is { } entered)
        {
            return Focus(tab, layout, entered, tab.VisibleRows);
        }

        var current = CurrentItem(tab, layout);
        var moved = ListMotion.Move(new ListPosition(current, 0), move, 1, count, tab.VisibleRows);
        var target = layout.ItemAt(moved.Selected);

        if (target.SprintId is { } sprintId && layout.EmptySprint(sprintId) is { } section)
        {
            return Focus(tab, layout, section, tab.VisibleRows);
        }

        return Place(tab, layout, target.Row, tab.VisibleRows);
    }

    /// <summary>
    /// Puts the cursor on the header of the empty sprint <paramref name="sprintId"/> (a click on it); the tab as it is
    /// when that sprint has rows or is unknown.
    /// </summary>
    public static TabState FocusSprint(TabState tab, long sprintId)
    {
        var layout = BacklogSections.Build(tab);

        return layout.EmptySprint(sprintId) is { } section ? Focus(tab, layout, section, tab.VisibleRows) : tab;
    }

    /// <summary>
    /// The empty sprint a click on display line <paramref name="line"/> (header, goal or note) points at.
    /// </summary>
    public static long? EmptySprintAtLine(BacklogLayout layout, int line)
    {
        if (layout.LineAt(line) is not { Row: null, Section: { HasRows: false, Sprint: { } sprint } })
        {
            return null;
        }

        return sprint.Id;
    }

    /// <summary>
    /// The first row of the next section that has rows (or of the current one and then the previous when
    /// <paramref name="direction"/> is negative); the tab as it is when there is none.
    /// </summary>
    public static TabState Jump(TabState tab, int direction)
    {
        if (tab.Rows.Count == 0)
        {
            return tab;
        }

        var layout = BacklogSections.Build(tab);
        var current = layout.StopFor(Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1));
        int? target = null;

        foreach (var section in layout.Sections)
        {
            if (!section.HasRows)
            {
                continue;
            }

            if (direction > 0 && section.FirstRow > current)
            {
                target = section.FirstRow;

                break;
            }

            if (direction < 0 && section.FirstRow < current)
            {
                target = section.FirstRow;
            }
        }

        return target is { } row ? Place(tab, layout, row, tab.VisibleRows) : tab;
    }

    /// <summary>
    /// The click on a display line of the body: the row of a row line, the first row of the section for its header or
    /// goal line, null for an empty note and for a line past the end.
    /// </summary>
    public static int? RowAtLine(BacklogLayout layout, int line)
    {
        if (layout.LineAt(line) is not { } found)
        {
            return null;
        }

        if (found.Row is { } row)
        {
            return row;
        }

        return found.Section.HasRows ? found.Section.FirstRow : null;
    }

    /// <summary>
    /// Collapses the section the cursor is in, or expands it. A collapsed section keeps its first row as the cursor
    /// stop. The tab as it is when no row exists.
    /// </summary>
    public static TabState Toggle(TabState tab)
    {
        if (tab.Rows.Count == 0)
        {
            return tab;
        }

        var layout = BacklogSections.Build(tab);
        var row = Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1);

        if (layout.SectionOfRow(row) is not { } section)
        {
            return tab;
        }

        var keys = section.Collapsed
            ? tab.CollapsedSections.Where(key => key != section.Key).ToArray()
            : [.. tab.CollapsedSections, section.Key];

        var toggled = tab with { CollapsedSections = keys };

        return Place(toggled, BacklogSections.Build(toggled), row, toggled.VisibleRows);
    }

    /// <summary>The cursor stands on the stop of a collapsed section: its header shows the cursor.</summary>
    public static bool OnCollapsedHeader(TabState tab)
    {
        if (tab.Rows.Count == 0 || !BacklogSections.Applies(tab))
        {
            return false;
        }

        var layout = BacklogSections.Build(tab);

        return layout.SectionOfRow(Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1)) is { Collapsed: true };
    }

    private static int CurrentItem(TabState tab, BacklogLayout layout)
    {
        if (tab.FocusedSprintId is { } focused && layout.ItemIndexOfSprint(focused) is >= 0 and var index)
        {
            return index;
        }

        if (tab.Rows.Count == 0)
        {
            return 0;
        }

        return layout.ItemIndexOfRow(Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1));
    }

    private static TabState Focus(TabState tab, BacklogLayout layout, BacklogSection section, int viewport)
    {
        var row = tab.Rows.Count == 0 ? 0 : Math.Clamp(section.FirstRow, 0, tab.Rows.Count - 1);
        var position = ListMotion.Reveal(new ListPosition(section.FirstLine, tab.Scroll), layout.LineCount, viewport);
        var key = tab.Rows.Count == 0 ? null : tab.Rows[row].Key;

        return tab with
        {
            Selected = row,
            SelectedKey = key,
            Scroll = Math.Min(position.Scroll, section.FirstLine),
            FocusedSprintId = section.Sprint!.Id,
        };
    }

    private static TabState Place(TabState tab, BacklogLayout layout, int row, int viewport)
    {
        var stop = layout.StopFor(row);
        var section = layout.SectionOfRow(stop);
        var line = layout.LineOfRow(stop);
        var position = ListMotion.Reveal(new ListPosition(line, tab.Scroll), layout.LineCount, viewport);
        var scroll = position.Scroll;

        // The first row of a section brings its header along when the window moves up to it.
        if (section is not null && stop == section.FirstRow && scroll > section.FirstLine)
        {
            scroll = section.FirstLine;
        }

        return tab with { Selected = stop, SelectedKey = tab.Rows[stop].Key, Scroll = scroll, FocusedSprintId = null };
    }
}
