using System.Text.Json.Serialization;

namespace Geeratya.Core.SavedViews;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SavedViewList))]
internal sealed partial class SavedViewsJsonContext : JsonSerializerContext;
