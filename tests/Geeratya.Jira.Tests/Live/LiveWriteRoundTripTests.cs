using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Live;

/// <summary>
/// Create, edit, comment, transition and delete on the team-managed sandbox project. Deletes only happen in GTM
/// (GCM rejects them, issue #19). The issue carries the label <c>geeratya-test-&lt;run id&gt;</c> and is deleted at
/// the end even when a step fails.
/// </summary>
public class LiveWriteRoundTripTests
{
    private const string Project = "GTM";
    private const string IssuePath = "/rest/api/3/issue";
    private const string CommentBody =
        """{"body":{"type":"doc","version":1,"content":[{"type":"paragraph","content":[{"type":"text","text":"live"}]}]}}""";

    [LiveFact]
    public async Task GivenSandboxProject_WhenRoundTripping_ThenEachWriteTakesEffectAndIssueIsDeleted()
    {
        var environment = LiveEnvironment.Load();
        using var transport = environment.CreateTransport();
        var runId = Guid.NewGuid().ToString("N")[..8];
        var token = CancellationToken.None;

        var created = await transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Post, IssuePath, CreateBody(runId)),
            LiveWriteJsonContext.Default.LiveCreatedIssue,
            token);
        Assert.True(created.IsSuccess, Describe(created.IsSuccess ? null : created.Error));
        var path = $"{IssuePath}/{created.Value.Key}";

        WriteResult<WriteAccepted> deleted;

        try
        {
            await EditCommentAndTransition(transport, path, runId, token);
        }
        finally
        {
            // Asserted after the try so a failed step is not hidden by a cleanup failure.
            deleted = await transport.SendWriteAsync(JiraRequest.Write(HttpMethod.Delete, path, null), token);
        }

        Assert.True(deleted.IsSuccess, Describe(deleted.IsSuccess ? null : deleted.Error));

        var gone = await transport.SendAsync(
            JiraRequest.Get(path),
            LiveWriteJsonContext.Default.LiveIssueState,
            token);
        Assert.False(gone.IsSuccess);
        Assert.Equal(JiraErrorKind.NotFound, gone.Error.Kind);
    }

    private static async Task EditCommentAndTransition(
        JiraTransport transport,
        string path,
        string runId,
        CancellationToken cancellationToken)
    {
        var edited = await transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Put, path, """{"fields":{"summary":"geeratya live edited"}}"""),
            cancellationToken);
        Assert.True(edited.IsSuccess, Describe(edited.IsSuccess ? null : edited.Error));

        var added = await transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Post, $"{path}/comment", CommentBody),
            LiveWriteJsonContext.Default.LiveCreatedComment,
            cancellationToken);
        Assert.True(added.IsSuccess, Describe(added.IsSuccess ? null : added.Error));
        var commentPath = $"{path}/comment/{added.Value.Id}";

        var commentEdited = await transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Put, commentPath, CommentBody.Replace("live", "live again", StringComparison.Ordinal)),
            LiveWriteJsonContext.Default.LiveCreatedComment,
            cancellationToken);
        Assert.True(commentEdited.IsSuccess, Describe(commentEdited.IsSuccess ? null : commentEdited.Error));

        var commentDeleted = await transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Delete, commentPath, null),
            cancellationToken);
        Assert.True(commentDeleted.IsSuccess, Describe(commentDeleted.IsSuccess ? null : commentDeleted.Error));

        var target = await FirstOtherStatus(transport, path, cancellationToken);
        var moved = await transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Post, $"{path}/transitions", $$$"""{"transition":{"id":"{{{target.Id}}}"}}"""),
            cancellationToken);
        Assert.True(moved.IsSuccess, Describe(moved.IsSuccess ? null : moved.Error));

        var read = await transport.SendAsync(
            JiraRequest.Get($"{path}?fields=summary,status,labels"),
            LiveWriteJsonContext.Default.LiveIssueState,
            cancellationToken);
        Assert.True(read.IsSuccess);
        Assert.Equal("geeratya live edited", read.Value.Fields.Summary);
        Assert.Equal(target.To.Name, read.Value.Fields.Status.Name);
        Assert.Contains($"geeratya-test-{runId}", read.Value.Fields.Labels);
    }

    private static async Task<LiveTransition> FirstOtherStatus(
        JiraTransport transport,
        string path,
        CancellationToken cancellationToken)
    {
        var current = await transport.SendAsync(
            JiraRequest.Get($"{path}?fields=summary,status,labels"),
            LiveWriteJsonContext.Default.LiveIssueState,
            cancellationToken);
        var list = await transport.SendAsync(
            JiraRequest.Get($"{path}/transitions"),
            LiveWriteJsonContext.Default.LiveTransitionList,
            cancellationToken);
        Assert.True(current.IsSuccess && list.IsSuccess);

        return list.Value.Transitions.First(entry => entry.To.Name != current.Value.Fields.Status.Name);
    }

    private static string CreateBody(string runId) =>
        $$$"""{"fields":{"project":{"key":"{{{Project}}}"},"issuetype":{"name":"Task"},"summary":"geeratya live round trip","labels":["geeratya-test-{{{runId}}}"]}}""";

    private static string Describe(WriteError? error) =>
        error is null ? string.Empty : $"Write failed: {error.Kind} (HTTP {error.StatusCode}).";
}
