using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Sprints;

/// <summary>The sprints of a board arrived for the open sprint switcher.</summary>
public sealed record SprintsLoaded(long BoardId, IReadOnlyList<Sprint> Sprints) : SourceCommand;

/// <summary>The sprints of a board could not be loaded.</summary>
public sealed record SprintsFailed(long BoardId, JiraError Error) : SourceCommand;

/// <summary>Load every sprint of a Scrum board for the sprint switcher; the host answers with the two commands above.</summary>
public sealed record LoadSprintsEffect(long BoardId) : Effect;
