using Geeratya.Core.State;

namespace Geeratya.Core.Cache;

/// <summary>A cache file could not be read or written; a one-line notice, the data is still loaded from Jira.</summary>
public sealed record CacheProblem(string Message) : Command;
