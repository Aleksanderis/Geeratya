using System.Text.Json.Serialization;

namespace Geeratya.Jira.Sprints;

/// <summary>Source-generated JSON metadata of the sprint write client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SprintWriteWire.Sprint))]
internal sealed partial class SprintWriteJsonContext : JsonSerializerContext;
