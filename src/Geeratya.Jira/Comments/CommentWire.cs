using System.Text.Json;

namespace Geeratya.Jira.Comments;

/// <summary>
/// The JSON shapes of <c>GET /issue/{key}/comment</c>. Every member is optional; the body stays raw JSON for the
/// ADF parser.
/// </summary>
internal static class CommentWire
{
    internal sealed record Page(int? StartAt, int? MaxResults, int? Total, List<Comment?>? Comments);

    internal sealed record Comment(string? Id, Author? Author, JsonElement? Body, string? Created, string? Updated);

    internal sealed record Author(string? AccountId, string? DisplayName);
}
