using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Geeratya.Tui.Tests.Fakes;

/// <summary>
/// An in-process Jira that answers each request with the next scripted reply and records the request URIs. A reply
/// with a gate is held until the gate completes. Never opens a socket.
/// </summary>
public sealed class ScriptedJiraHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<ScriptedReply> _replies = new();
    private readonly ConcurrentQueue<Uri> _requests = new();

    public IReadOnlyCollection<Uri> Requests => _requests;

    public ScriptedJiraHandler Reply(HttpStatusCode status, string body, Task? gate = null)
    {
        _replies.Enqueue(new ScriptedReply(status, body, gate, null));

        return this;
    }

    public ScriptedJiraHandler RateLimit(string retryAfter)
    {
        _replies.Enqueue(new ScriptedReply((HttpStatusCode)429, string.Empty, null, retryAfter));

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Enqueue(request.RequestUri!);

        if (!_replies.TryDequeue(out var reply))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (reply.Gate is not null)
        {
            await reply.Gate.WaitAsync(cancellationToken);
        }

        var response = new HttpResponseMessage(reply.Status)
        {
            Content = new StringContent(reply.Body, Encoding.UTF8, "application/json"),
        };

        if (reply.RetryAfter is not null)
        {
            response.Headers.TryAddWithoutValidation("Retry-After", reply.RetryAfter);
        }

        return response;
    }

    private sealed record ScriptedReply(HttpStatusCode Status, string Body, Task? Gate, string? RetryAfter);
}
