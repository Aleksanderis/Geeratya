using Geeratya.Core.State;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Filters.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Sources;

/// <summary>A command handled by <see cref="SourceReducer"/>.</summary>
public abstract record SourceCommand : Command;

/// <summary>The site's boards arrived.</summary>
public sealed record BoardsLoaded(IReadOnlyList<BoardSummary> Boards) : SourceCommand;

/// <summary>The site's saved filters arrived (favourites first, no duplicates).</summary>
public sealed record FiltersLoaded(IReadOnlyList<SavedFilter> Filters) : SourceCommand;

/// <summary>A list failed to load.</summary>
public sealed record SourceListFailed(SourceListKind Kind, JiraError Error) : SourceCommand;
