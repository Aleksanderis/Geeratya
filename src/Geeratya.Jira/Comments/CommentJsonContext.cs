using System.Text.Json.Serialization;

namespace Geeratya.Jira.Comments;

/// <summary>Source-generated JSON metadata of the comment client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CommentWire.Page))]
[JsonSerializable(typeof(CommentWire.Comment))]
internal sealed partial class CommentJsonContext : JsonSerializerContext;
