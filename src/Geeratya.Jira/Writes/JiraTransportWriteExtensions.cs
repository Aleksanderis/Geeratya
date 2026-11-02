using System.Text.Json.Serialization.Metadata;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Writes;

/// <summary>Sends writes through a <see cref="JiraTransport"/> and returns a typed <see cref="WriteResult{T}"/>.</summary>
public static class JiraTransportWriteExtensions
{
    /// <summary>
    /// Sends a write and parses a JSON reply of <typeparamref name="T"/>. Never retried, except on a 429 that names
    /// a <c>Retry-After</c> (the request was not processed). A timeout, network failure, 5xx or unreadable reply
    /// is an unknown outcome; a cancelled call throws <see cref="OperationCanceledException"/>, also unknown.
    /// </summary>
    public static async Task<WriteResult<T>> SendWriteAsync<T>(
        this JiraTransport transport,
        JiraRequest request,
        JsonTypeInfo<T> resultInfo,
        CancellationToken cancellationToken)
    {
        RequireWrite(request);
        var result = await transport.SendUncheckedAsync(request, resultInfo, cancellationToken);

        return WriteResults.From(result);
    }

    /// <summary>Sends a write whose reply body is not read (204 or an ignored body); same rules as above.</summary>
    public static async Task<WriteResult<WriteAccepted>> SendWriteAsync(
        this JiraTransport transport,
        JiraRequest request,
        CancellationToken cancellationToken)
    {
        RequireWrite(request);
        var result = await transport.SendUncheckedAsync<WriteAccepted>(request, null, cancellationToken);

        return WriteResults.From(result);
    }

    private static void RequireWrite(JiraRequest request)
    {
        if (request.IsIdempotent)
        {
            throw new ArgumentException("A write must be built with JiraRequest.Write.", nameof(request));
        }
    }
}
