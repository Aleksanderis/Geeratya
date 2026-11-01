using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geeratya.Core.Keymap;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(KeymapFile))]
internal sealed partial class KeymapJsonContext : JsonSerializerContext;
