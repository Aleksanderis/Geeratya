using System.Text.Json.Serialization;

namespace Geeratya.Jira.Fields;

/// <summary>Source-generated JSON metadata of the field client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<FieldWire.Field>))]
internal sealed partial class FieldJsonContext : JsonSerializerContext;
