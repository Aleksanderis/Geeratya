namespace Geeratya.Core.BoardKinds;

/// <summary>The stored probe answers of one profile: board id to <c>scrum</c> or <c>kanban</c>.</summary>
public sealed record BoardKindFile(Dictionary<string, string> Boards);
