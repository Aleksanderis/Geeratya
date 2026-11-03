using System.Text.Json.Serialization;

namespace Geeratya.Core.BoardKinds;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(BoardKindFile))]
internal sealed partial class BoardKindJsonContext : JsonSerializerContext;
