namespace Geeratya.Jira.Paging;

/// <summary>An opaque token that continues a token-paged search. Only valid with the query that produced it.</summary>
public sealed record PageCursor(string Token);
