using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geeratya.Core.Keymap;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(KeymapFile))]
[JsonSerializable(typeof(UserKeymapFile))]
internal sealed partial class KeymapJsonContext : JsonSerializerContext;
