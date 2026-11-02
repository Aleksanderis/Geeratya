using System.Text.Json.Serialization;

namespace Geeratya.Jira.Edit;

/// <summary>Source-generated JSON metadata of the edit client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EditWire.Meta))]
internal sealed partial class EditJsonContext : JsonSerializerContext;
