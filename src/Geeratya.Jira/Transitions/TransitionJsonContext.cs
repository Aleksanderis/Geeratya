using System.Text.Json.Serialization;

namespace Geeratya.Jira.Transitions;

/// <summary>Source-generated JSON metadata of the transition client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TransitionWire.Page))]
internal sealed partial class TransitionJsonContext : JsonSerializerContext;
