using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Backlog;

/// <summary>
/// One section of the Scrum backlog: a sprint or the backlog itself. A section covers the rows
/// <see cref="FirstRow"/> to <c>FirstRow + RowCount - 1</c> of the tab and, in the display, the lines
/// <see cref="FirstLine"/> to <c>FirstLine + LineCount - 1</c>: the header, an optional goal line, then the rows
/// (or one note line when there are none). A collapsed section shows only its header.
/// </summary>
/// <param name="Key">Stable id: <c>sprint:&lt;id&gt;</c> or <c>backlog</c>; the collapsed state is kept by it.</param>
/// <param name="Name">The sprint name, or the backlog's name.</param>
/// <param name="Sprint">
/// The sprint's metadata when the sprint list arrived; null for the backlog and before that.
/// </param>
/// <param name="State">The sprint state; null for the backlog.</param>
/// <param name="FirstRow">The first row of the section; the position where rows would start when it is empty.</param>
/// <param name="RowCount">How many loaded rows the section holds.</param>
/// <param name="Points">The sum of the estimates of its rows.</param>
/// <param name="HasEstimates">At least one row carries an estimate.</param>
/// <param name="Collapsed">Only the header shows.</param>
/// <param name="FirstLine">The display line of the header.</param>
public sealed record BacklogSection(
    string Key,
    string Name,
    Sprint? Sprint,
    SprintState? State,
    int FirstRow,
    int RowCount,
    double Points,
    bool HasEstimates,
    bool Collapsed,
    int FirstLine)
{
    /// <summary>The goal line shows under the header of an expanded sprint that has a goal.</summary>
    public bool HasGoalLine => !Collapsed && !string.IsNullOrWhiteSpace(Sprint?.Goal);

    /// <summary>The lines this section takes in the display.</summary>
    public int LineCount => 1 + (HasGoalLine ? 1 : 0) + BodyLines;

    /// <summary>The display line of the first row, or of the note line of an empty section.</summary>
    public int FirstBodyLine => FirstLine + 1 + (HasGoalLine ? 1 : 0);

    /// <summary>The cursor can stand in the section: it has rows.</summary>
    public bool HasRows => RowCount > 0;

    /// <summary>The backlog section (no sprint).</summary>
    public bool IsBacklog => State is null;

    private int BodyLines => Collapsed ? 0 : Math.Max(1, RowCount);
}
