using System.Text.Json.Serialization;

namespace Geeratya.Jira.Links;

/// <summary>Source-generated JSON metadata of the link client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LinkWire.Types))]
internal sealed partial class LinkJsonContext : JsonSerializerContext;
