using System.Text.Json.Serialization;
using Geeratya.Jira.Boards;

namespace Geeratya.Jira.Sprints;

/// <summary>Source-generated JSON metadata of the sprint client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SprintWire.SprintList))]
[JsonSerializable(typeof(SprintWire.Sprint))]
[JsonSerializable(typeof(BoardWire.IssuePage))]
internal sealed partial class SprintJsonContext : JsonSerializerContext;
