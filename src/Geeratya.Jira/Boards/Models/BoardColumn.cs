namespace Geeratya.Jira.Boards.Models;

/// <summary>A board column: its name and the ids of the statuses it holds. Names of statuses come from the project.</summary>
public sealed record BoardColumn(string Name, IReadOnlyList<string> StatusIds);
