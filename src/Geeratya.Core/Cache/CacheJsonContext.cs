using System.Text.Json.Serialization;

namespace Geeratya.Core.Cache;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CachedViewFile))]
[JsonSerializable(typeof(CachedIssueFile))]
public sealed partial class CacheJsonContext : JsonSerializerContext;
