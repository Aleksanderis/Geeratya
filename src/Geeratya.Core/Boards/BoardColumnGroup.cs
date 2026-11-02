using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Boards;

/// <summary>The issues of one board column, in the order they were loaded.</summary>
/// <param name="Name">The column name from the board configuration.</param>
/// <param name="Rows">The issues whose status the column holds.</param>
public sealed record BoardColumnGroup(string Name, IReadOnlyList<IssueSummary> Rows);
