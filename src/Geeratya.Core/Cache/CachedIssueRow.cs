namespace Geeratya.Core.Cache;

/// <summary>The JSON shape of a cached list row (see <see cref="CachedViewFile"/>).</summary>
public sealed record CachedIssueRow(
    string Id,
    string Key,
    string Summary,
    CachedStatus Status,
    CachedIssueType IssueType,
    string? Priority,
    CachedPerson? Assignee,
    List<string> Labels,
    CachedParent? Parent,
    DateTimeOffset? Updated,
    string ProjectKey,
    List<CachedSprint> Sprints,
    double? Estimate,
    List<CachedSubtask>? Subtasks = null);

/// <summary>The JSON shape of an issue status.</summary>
public sealed record CachedStatus(string Id, string Name, string CategoryKey);

/// <summary>The JSON shape of an issue type.</summary>
public sealed record CachedIssueType(string Id, string Name, int HierarchyLevel, bool IsSubtask);

/// <summary>The JSON shape of an assignee.</summary>
public sealed record CachedPerson(string AccountId, string DisplayName);

/// <summary>The JSON shape of a parent stub.</summary>
public sealed record CachedParent(string Key, string? Summary, bool IsEpic);

/// <summary>The JSON shape of a sprint of an issue; <see cref="State"/> is the enum member name.</summary>
public sealed record CachedSprint(long Id, string Name, string State);

/// <summary>The JSON shape of a sub-task stub of a cached row.</summary>
public sealed record CachedSubtask(string Key, string Summary, CachedStatus Status, string TypeName);
