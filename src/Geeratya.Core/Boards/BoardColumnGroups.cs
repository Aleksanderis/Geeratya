using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Boards;

/// <summary>Loaded issues sorted into the columns of a board.</summary>
/// <param name="Columns">One group per configured column, in board order (empty columns included).</param>
/// <param name="Unmapped">Issues whose status is in no column; a layout may ignore or show them.</param>
public sealed record BoardColumnGroups(IReadOnlyList<BoardColumnGroup> Columns, IReadOnlyList<IssueSummary> Unmapped);
