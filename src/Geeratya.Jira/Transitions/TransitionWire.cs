using System.Text.Json;

namespace Geeratya.Jira.Transitions;

/// <summary>
/// The JSON shapes of <c>GET /issue/{key}/transitions?expand=transitions.fields</c>. Every member is optional.
/// </summary>
internal static class TransitionWire
{
    internal sealed record Page(List<Item?>? Transitions);

    internal sealed record Item(
        string? Id,
        string? Name,
        Status? To,
        bool? HasScreen,
        bool? IsGlobal,
        bool? IsAvailable,
        Dictionary<string, Field?>? Fields);

    internal sealed record Status(string? Id, string? Name, Category? StatusCategory);

    internal sealed record Category(string? Key);

    internal sealed record Field(
        string? FieldId,
        string? Name,
        bool? Required,
        Schema? Schema,
        bool? HasDefaultValue,
        List<JsonElement>? AllowedValues);

    internal sealed record Schema(string? Type, string? Items, string? System, string? Custom);
}
