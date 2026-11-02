using System.Text.Json.Serialization;

namespace Geeratya.Jira.Filters;

/// <summary>Source-generated JSON metadata of the filter client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FilterWire.FilterPage))]
[JsonSerializable(typeof(FilterWire.Filter))]
[JsonSerializable(typeof(List<FilterWire.Filter>))]
internal sealed partial class FilterJsonContext : JsonSerializerContext;
