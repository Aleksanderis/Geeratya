using System.Text.Json.Serialization;

namespace Geeratya.Jira.RemoteLinks;

/// <summary>Source-generated JSON metadata of the remote link client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<RemoteLinkWire.Link?>))]
internal sealed partial class RemoteLinkJsonContext : JsonSerializerContext;
