using System.Text.Json;

namespace Geeratya.Jira.Edit;

/// <summary>The JSON shapes of <c>GET /issue/{key}/editmeta</c>. Every member is optional.</summary>
internal static class EditWire
{
    internal sealed record Meta(Dictionary<string, Field?>? Fields);

    internal sealed record Field(
        string? Key,
        string? Name,
        bool? Required,
        Schema? Schema,
        List<string>? Operations,
        bool? HasDefaultValue,
        string? AutoCompleteUrl,
        List<JsonElement>? AllowedValues);

    internal sealed record Schema(string? Type, string? Items, string? System, string? Custom);
}
