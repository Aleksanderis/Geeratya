using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geeratya.Tui.Rendering;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(ThemeFileModel))]
internal sealed partial class ThemeJsonContext : JsonSerializerContext;
