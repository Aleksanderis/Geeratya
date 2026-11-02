using System.Text.Json.Serialization;

namespace Geeratya.Jira.Rank;

/// <summary>Source-generated JSON metadata of the rank client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RankWire.Response))]
internal sealed partial class RankJsonContext : JsonSerializerContext;
