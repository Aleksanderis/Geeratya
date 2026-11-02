using System.Globalization;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Comments;

/// <summary>
/// Reads the comments of one issue, one offset page at a time, oldest first, and adds, edits and deletes one.
/// Reading is a read; <see cref="AddCommentAsync"/>, <see cref="EditCommentAsync"/> and
/// <see cref="DeleteCommentAsync"/> are writes: call them only from a confirmed command, never retry them
/// silently (a repeated add duplicates), and reload with <see cref="ReadAfterWrite"/> after an unknown outcome.
/// The client keeps no cache. A missing or hidden issue is a <see cref="JiraErrorKind.NotFound"/> ("not found or
/// no access"); editing or deleting someone else's comment is <see cref="WriteErrorKind.Permission"/> (403).
/// </summary>
public sealed class CommentClient(JiraTransport transport)
{
    /// <summary>Default and largest page size requested.</summary>
    public const int MaxPageSize = 100;

    private const string IssuePath = "/rest/api/3/issue";

    /// <summary>
    /// Loads the page starting at <paramref name="startAt"/>; continue with <see cref="OffsetPage{T}.NextStartAt"/>
    /// until it is null.
    /// </summary>
    public async Task<JiraResult<OffsetPage<IssueComment>>> GetCommentsAsync(
        string key,
        int startAt,
        CancellationToken cancellationToken,
        int pageSize = MaxPageSize,
        JiraPriority priority = JiraPriority.User)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return JiraResults.Failure<OffsetPage<IssueComment>>(
                new JiraError(JiraErrorKind.NotFound, null, "Not found or no access."));
        }

        var start = Math.Max(startAt, 0);
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var url = $"{IssuePath}/{key}/comment?orderBy=created&startAt={Number(start)}&maxResults={Number(size)}";

        var result = await transport.SendAsync(
            JiraRequest.Get(url) with { Priority = priority },
            CommentJsonContext.Default.Page,
            cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(CommentMapper.MapPage(result.Value, start, size))
            : JiraResults.Failure<OffsetPage<IssueComment>>(result.Error);
    }

    /// <summary>
    /// Adds a comment with an ADF body (visibility stays at Jira's default) and returns the stored comment.
    /// </summary>
    public Task<WriteResult<IssueComment>> AddCommentAsync(
        string key,
        AdfDocument body,
        CancellationToken cancellationToken) =>
        SendBodyAsync(HttpMethod.Post, key, null, body, cancellationToken);

    /// <summary>Replaces the body of a comment and returns the stored comment (author, created, updated).</summary>
    public Task<WriteResult<IssueComment>> EditCommentAsync(
        string key,
        string commentId,
        AdfDocument body,
        CancellationToken cancellationToken) =>
        SendBodyAsync(HttpMethod.Put, key, commentId, body, cancellationToken);

    /// <summary>Deletes a comment. Jira has no undo; the reply carries no body (204).</summary>
    public Task<WriteResult<WriteAccepted>> DeleteCommentAsync(
        string key,
        string commentId,
        CancellationToken cancellationToken)
    {
        if (Refusal<WriteAccepted>(key, commentId) is { } refused)
        {
            return Task.FromResult(refused);
        }

        var request = JiraRequest.Write(HttpMethod.Delete, CommentPath(key, commentId), null);

        return transport.SendWriteAsync(request, cancellationToken);
    }

    private async Task<WriteResult<IssueComment>> SendBodyAsync(
        HttpMethod method,
        string key,
        string? commentId,
        AdfDocument body,
        CancellationToken cancellationToken)
    {
        if (Refusal<IssueComment>(key, commentId, commentId is not null) is { } refused)
        {
            return refused;
        }

        var json = $$"""{"body":{{AdfJsonWriter.ToJson(body)}}}""";
        var result = await transport.SendWriteAsync(
            JiraRequest.Write(method, CommentPath(key, commentId), json),
            CommentJsonContext.Default.Comment,
            cancellationToken);

        return result.IsSuccess
            ? WriteResults.Success(CommentMapper.Map(result.Value))
            : WriteResults.Failure<IssueComment>(result.Error);
    }

    private static string CommentPath(string key, string? commentId) =>
        commentId is null ? $"{IssuePath}/{key}/comment" : $"{IssuePath}/{key}/comment/{commentId}";

    // Refused before sending: a bad key or id would otherwise address another resource.
    private static WriteResult<T>? Refusal<T>(string key, string? commentId, bool needsId = true)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return Refused<T>(WriteErrorKind.NotFound, "Not found or no access.");
        }

        return needsId && (commentId is null || commentId.Length is 0 or > 20 || !commentId.All(char.IsAsciiDigit))
            ? Refused<T>(WriteErrorKind.NotFound, "Comment not found.")
            : null;
    }

    private static WriteResult<T> Refused<T>(WriteErrorKind kind, string message) =>
        WriteResults.Failure<T>(new WriteError(kind, null, message, [], []));

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
