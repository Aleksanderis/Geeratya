using System.Text.Json;

namespace Geeratya.Jira.Create;

/// <summary>The JSON shapes of the create metadata and the create reply. Every member is optional.</summary>
internal static class CreateWire
{
    internal sealed record IssueTypePage(int? StartAt, int? MaxResults, int? Total, List<IssueType?>? IssueTypes);

    internal sealed record IssueType(string? Id, string? Name, string? Description, int? HierarchyLevel, bool? Subtask);

    internal sealed record FieldPage(int? StartAt, int? MaxResults, int? Total, List<Field?>? Fields);

    internal sealed record Field(
        string? FieldId,
        string? Name,
        bool? Required,
        bool? HasDefaultValue,
        Schema? Schema,
        List<JsonElement>? AllowedValues);

    internal sealed record Schema(string? Type, string? Items, string? System, string? Custom);

    internal sealed record Created(string? Id, string? Key);
}
