using System.Text.Json.Serialization;

namespace Geeratya.Jira.Search;

/// <summary>Source-generated JSON metadata of the search client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SearchWire.Response))]
[JsonSerializable(typeof(SearchWire.Body))]
[JsonSerializable(typeof(SearchWire.CountBody))]
[JsonSerializable(typeof(SearchWire.CountResponse))]
internal sealed partial class SearchJsonContext : JsonSerializerContext;
