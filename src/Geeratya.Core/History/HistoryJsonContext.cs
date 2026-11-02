using System.Text.Json.Serialization;

namespace Geeratya.Core.History;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SiteHistory))]
internal sealed partial class HistoryJsonContext : JsonSerializerContext;
