using System.Text.Json.Serialization;

namespace Geeratya.Jira.Create;

/// <summary>Source-generated JSON metadata of the create client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CreateWire.IssueTypePage))]
[JsonSerializable(typeof(CreateWire.FieldPage))]
[JsonSerializable(typeof(CreateWire.Created))]
internal sealed partial class CreateJsonContext : JsonSerializerContext;
