namespace Geeratya.Core.Backlog;

/// <summary>
/// The sections of a Scrum backlog tab laid out as display lines, built from the tab's rows, the sprint list and the
/// collapsed sections. The cursor stands on rows only: the stops are every row of an expanded section and the first
/// row of a collapsed one (its header stands in for it), so motions cross sections without special cases.
/// </summary>
public sealed class BacklogLayout
{
    private readonly int[] _stops;
    private readonly CursorItem[] _empties;
    private readonly int[] _emptyItemIndexes;

    public BacklogLayout(IReadOnlyList<BacklogSection> sections)
    {
        Sections = sections;
        LineCount = sections.Sum(section => section.LineCount);
        _stops = BuildStops(sections);
        _empties = BuildEmpties(sections);
        _emptyItemIndexes = BuildEmptyItemIndexes();
    }

    public IReadOnlyList<BacklogSection> Sections { get; }

    /// <summary>All display lines.</summary>
    public int LineCount { get; }

    /// <summary>The rows the cursor can stand on, ascending.</summary>
    public IReadOnlyList<int> Stops => _stops;

    /// <summary>
    /// How many places the keyboard cursor visits: the stops, and the header of every empty sprint section (known by
    /// its sprint) at its place between them. Items are addressed by <see cref="ItemAt"/>, never materialized, so a
    /// build costs work proportional to the sections, not the rows.
    /// </summary>
    public int ItemCount => _stops.Length + _empties.Length;

    /// <summary>The item at <paramref name="index"/> of the cursor order, 0 to <see cref="ItemCount"/> minus 1.</summary>
    public CursorItem ItemAt(int index)
    {
        var before = Array.BinarySearch(_emptyItemIndexes, index);

        if (before >= 0)
        {
            return _empties[before];
        }

        return new CursorItem(_stops[index - ~before], null);
    }

    /// <summary>The item index of the empty sprint <paramref name="sprintId"/>, -1 when it is not one.</summary>
    public int ItemIndexOfSprint(long sprintId)
    {
        for (var index = 0; index < _empties.Length; index++)
        {
            if (_empties[index].SprintId == sprintId)
            {
                return _emptyItemIndexes[index];
            }
        }

        return -1;
    }

    /// <summary>The item index of the stop that <paramref name="row"/> stands for (0 when it has none).</summary>
    public int ItemIndexOfRow(int row)
    {
        var stopIndex = StopIndexOf(row);

        if (_stops.Length == 0)
        {
            return 0;
        }

        var emptiesBefore = 0;

        while (emptiesBefore < _empties.Length && _empties[emptiesBefore].Row <= _stops[stopIndex])
        {
            emptiesBefore++;
        }

        return stopIndex + emptiesBefore;
    }

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

    private static CursorItem[] BuildEmpties(IReadOnlyList<BacklogSection> sections)
    {
        var empties = new List<CursorItem>();

        foreach (var section in sections)
        {
            if (!section.HasRows && section.Sprint is { } sprint)
            {
                empties.Add(new CursorItem(section.FirstRow, sprint.Id));
            }
        }

        return [.. empties];
    }

    private int[] BuildEmptyItemIndexes()
    {
        var indexes = new int[_empties.Length];

        for (var index = 0; index < _empties.Length; index++)
        {
            var stopsBefore = Array.BinarySearch(_stops, _empties[index].Row);

            indexes[index] = index + (stopsBefore >= 0 ? stopsBefore : ~stopsBefore);
        }

        return indexes;
    }
}
