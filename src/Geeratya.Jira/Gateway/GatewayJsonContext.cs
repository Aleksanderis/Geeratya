using System.Text.Json.Serialization;

namespace Geeratya.Jira.Gateway;

/// <summary>Source-generated JSON metadata of the gateway client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GatewayWire.TenantInfo))]
[JsonSerializable(typeof(GatewayWire.IdsRequest))]
[JsonSerializable(typeof(GatewayWire.IdRequest))]
[JsonSerializable(typeof(GatewayWire.NavigationReply))]
[JsonSerializable(typeof(GatewayWire.CutoffReply))]
internal sealed partial class GatewayJsonContext : JsonSerializerContext;
