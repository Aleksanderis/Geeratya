using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>The sprint a tab points at, or why there is none.</summary>
public sealed record SprintLookup(Sprint? Sprint, string Reason);
