using Geeratya.Core.State;

namespace Geeratya.Core.Cache;

/// <summary>The cache could not be written; shown as a one-line notice, the data is still loaded from Jira.</summary>
public sealed record CacheWriteFailed(string Message) : Command;
