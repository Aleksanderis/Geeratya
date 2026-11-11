using System.Text.Json.Serialization;

namespace Geeratya.Jira.Search;

/// <summary>
/// What a search row keeps of one site-specific field (Sprint, estimate, <c>subtasks</c>) after reading: the numeric
/// value or the typed array entries, never a <c>JsonElement</c>, so no per-issue <c>JsonDocument</c> buffer stays alive.
/// </summary>
/// <param name="Number">The value of a number field (estimate); null for any other shape.</param>
/// <param name="Sprints">The array entries that look like sprints (an object with a numeric id); null for none.</param>
/// <param name="Subtasks">The array entries that look like sub-task stubs (an object with a key); null for none.</param>
internal sealed record ExtraValue(double? Number, List<ExtraSprint>? Sprints, List<ExtraSubtask>? Subtasks);

/// <summary>A sprint entry of the Sprint field as sent.</summary>
internal sealed record ExtraSprint(long Id, string? Name, string? State);

/// <summary>A sub-task stub of the <c>subtasks</c> field as sent.</summary>
internal sealed record ExtraSubtask(string Key, ExtraSubtaskFields? Fields);

/// <summary>The fields of a sub-task stub.</summary>
internal sealed record ExtraSubtaskFields(
    string? Summary,
    SearchWire.Status? Status,
    [property: JsonPropertyName(IssueFields.IssueType)] SearchWire.IssueType? IssueType);
