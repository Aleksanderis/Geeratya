namespace Geeratya.Jira.Boards.Models;

/// <summary>A board in a list. <c>ProjectKey</c> is empty for a board that spans projects.</summary>
public sealed record BoardSummary(long Id, string Name, BoardType Type, string ProjectKey);
