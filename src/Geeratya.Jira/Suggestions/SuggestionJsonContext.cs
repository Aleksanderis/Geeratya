using System.Text.Json.Serialization;

namespace Geeratya.Jira.Suggestions;

/// <summary>Source-generated JSON metadata of the suggestion client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<SuggestionWire.User?>))]
[JsonSerializable(typeof(SuggestionWire.LabelPage))]
[JsonSerializable(typeof(SuggestionWire.Picker))]
[JsonSerializable(typeof(List<SuggestionWire.Named?>))]
internal sealed partial class SuggestionJsonContext : JsonSerializerContext;
