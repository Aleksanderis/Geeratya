using System.Text.Json.Serialization;

namespace Geeratya.Jira.Boards;

/// <summary>Source-generated JSON metadata of the board client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BoardWire.BoardList))]
[JsonSerializable(typeof(BoardWire.Board))]
[JsonSerializable(typeof(BoardWire.Configuration))]
[JsonSerializable(typeof(BoardWire.IssuePage))]
[JsonSerializable(typeof(BoardWire.QuickFilterList))]
internal sealed partial class BoardJsonContext : JsonSerializerContext;
