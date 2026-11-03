namespace Geeratya.Core.Backlog;

/// <summary>
/// The sections of a Scrum backlog tab laid out as display lines, built from the tab's rows, the sprint list and the
/// collapsed sections. The cursor stands on rows only: the stops are every row of an expanded section and the first
/// row of a collapsed one (its header stands in for it), so motions cross sections without special cases.
/// </summary>
public sealed class BacklogLayout
{
    private readonly int[] _stops;
    private readonly CursorItem[] _items;

    public BacklogLayout(IReadOnlyList<BacklogSection> sections)
    {
        Sections = sections;
        LineCount = sections.Sum(section => section.LineCount);
        _stops = BuildStops(sections);
        _items = BuildItems(sections);
    }

    public IReadOnlyList<BacklogSection> Sections { get; }

    /// <summary>All display lines.</summary>
    public int LineCount { get; }

    /// <summary>The rows the cursor can stand on, ascending.</summary>
    public IReadOnlyList<int> Stops => _stops;

    /// <summary>
    /// What the keyboard cursor visits, in display order: the stops, and the header of every empty sprint section
    /// (known by its sprint) at its place between them.
    /// </summary>
    public IReadOnlyList<CursorItem> Items => _items;

    /// <summary>
    /// The section of the empty sprint <paramref name="sprintId"/>, null when it has rows or is unknown.
    /// </summary>
    public BacklogSection? EmptySprint(long sprintId)
    {
        return Sections.FirstOrDefault(
            section => section is { HasRows: false, Sprint: { } sprint } && sprint.Id == sprintId);
    }

    /// <summary>The section that holds <paramref name="row"/>, null when no section does (no rows loaded).</summary>
    public BacklogSection? SectionOfRow(int row)
    {
        foreach (var section in Sections)
        {
            if (row >= section.FirstRow && row < section.FirstRow + section.RowCount)
            {
                return section;
            }
        }

        return null;
    }

    /// <summary>
    /// The row the cursor really stands on for <paramref name="row"/>: itself, or the first row of its section when
    /// that section is collapsed (the other rows are hidden).
    /// </summary>
    public int StopFor(int row)
    {
        var section = SectionOfRow(row);

        return section is { Collapsed: true } ? section.FirstRow : row;
    }

    /// <summary>
    /// The index in <see cref="Stops"/> of the stop for <paramref name="row"/>; 0 when there are none.
    /// </summary>
    public int StopIndexOf(int row)
    {
        var index = Array.BinarySearch(_stops, StopFor(row));

        return index >= 0 ? index : Math.Max(0, Math.Min(~index, _stops.Length - 1));
    }

    /// <summary>
    /// The display line the cursor shows on for <paramref name="row"/>: the header when it is collapsed.
    /// </summary>
    public int LineOfRow(int row)
    {
        var section = SectionOfRow(row);

        if (section is null)
        {
            return 0;
        }

        return section.Collapsed ? section.FirstLine : section.FirstBodyLine + (row - section.FirstRow);
    }

    /// <summary>
    /// What a display line is: the section it belongs to, and the row when it is a row line (null for a header, a goal
    /// line or an empty note). Null when the line is past the end.
    /// </summary>
    public BacklogLine? LineAt(int line)
    {
        foreach (var section in Sections)
        {
            if (line < section.FirstLine || line >= section.FirstLine + section.LineCount)
            {
                continue;
            }

            var offset = line - section.FirstBodyLine;
            var isRow = !section.Collapsed && offset >= 0 && offset < section.RowCount;

            return new BacklogLine(section, isRow ? section.FirstRow + offset : null);
        }

        return null;
    }

    private static int[] BuildStops(IReadOnlyList<BacklogSection> sections)
    {
        var stops = new List<int>();

        foreach (var section in sections)
        {
            if (!section.HasRows)
            {
                continue;
            }

            if (section.Collapsed)
            {
                stops.Add(section.FirstRow);

                continue;
            }

            for (var row = section.FirstRow; row < section.FirstRow + section.RowCount; row++)
            {
                stops.Add(row);
            }
        }

        return [.. stops];
    }

    private static CursorItem[] BuildItems(IReadOnlyList<BacklogSection> sections)
    {
        var items = new List<CursorItem>();

        foreach (var section in sections)
        {
            if (!section.HasRows)
            {
                if (section.Sprint is { } sprint)
                {
                    items.Add(new CursorItem(section.FirstRow, sprint.Id));
                }

                continue;
            }

            if (section.Collapsed)
            {
                items.Add(new CursorItem(section.FirstRow, null));

                continue;
            }

            for (var row = section.FirstRow; row < section.FirstRow + section.RowCount; row++)
            {
                items.Add(new CursorItem(row, null));
            }
        }

        return [.. items];
    }
}
