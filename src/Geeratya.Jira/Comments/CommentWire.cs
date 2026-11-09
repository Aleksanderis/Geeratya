using System.Text.Json;

namespace Geeratya.Jira.Comments;

/// <summary>
/// The JSON shapes of <c>GET /issue/{key}/comment</c>. Every member is optional; the body stays raw JSON for the
/// ADF parser.
/// </summary>
internal static class CommentWire
{
    internal sealed record Page(int? StartAt, int? MaxResults, int? Total, List<Comment?>? Comments);

    /// <summary>
    /// <c>ParentId</c> is a JSON number in Jira's reply (<c>"parentId":10024</c>) while <c>Id</c> is a string, so it is
    /// read as raw JSON and turned into the id text by the mapper.
    /// </summary>
    internal sealed record Comment(
        string? Id,
        Author? Author,
        JsonElement? Body,
        string? Created,
        string? Updated,
        JsonElement? ParentId = null);

    internal sealed record Author(string? AccountId, string? DisplayName);
}
