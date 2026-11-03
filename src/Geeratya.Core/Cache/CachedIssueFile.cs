namespace Geeratya.Core.Cache;

/// <summary>
/// The JSON shape of one cached issue file (<c>issue-&lt;hash&gt;.json</c>). Own DTOs, separate from the Jira
/// models, so a model change breaks <see cref="IssueCacheMapper"/> at compile time. A <c>Schema</c> other than
/// <see cref="IssueCacheMapper.Schema"/> is ignored; <c>SavedAt</c> is when the detail came from the network.
/// </summary>
public sealed record CachedIssueFile(
    int Schema,
    DateTimeOffset SavedAt,
    string Id,
    string Key,
    string Summary,
    CachedStatus Status,
    CachedIssueType IssueType,
    string? Priority,
    CachedPerson? Assignee,
    CachedPerson? Reporter,
    List<string> Labels,
    DateTimeOffset? Created,
    DateTimeOffset? Updated,
    string ProjectKey,
    CachedAdfDocument? Description,
    CachedStub? Parent,
    List<CachedStub> Subtasks,
    List<CachedLink> Links,
    List<CachedAttachment> Attachments,
    List<CachedSprint> Sprints,
    double? Estimate);

/// <summary>The JSON shape of a reference to another issue.</summary>
public sealed record CachedStub(string Key, string Summary, CachedStatus Status, CachedIssueType IssueType);

/// <summary>The JSON shape of an issue link; <see cref="Direction"/> is the enum member name.</summary>
public sealed record CachedLink(string Id, string TypeName, string Direction, string DirectionText, CachedStub Issue);

/// <summary>The JSON shape of attachment metadata (never the file).</summary>
public sealed record CachedAttachment(
    string Id,
    string FileName,
    string MimeType,
    long Size,
    DateTimeOffset? Created,
    CachedPerson? Author,
    string? ContentUrl);
