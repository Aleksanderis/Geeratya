using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Geeratya.Tui.Tests.Fakes;

/// <summary>
/// An in-process Jira that answers by request path, so parallel requests (an issue, its comments and its remote
/// links) get the right reply whatever order they arrive in. Replies come from recorded fixtures. Never opens a socket.
/// </summary>
public sealed class RoutedJiraHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, Route> _routes = new();
    private readonly ConcurrentQueue<Uri> _requests = new();

    public IReadOnlyCollection<Uri> Requests => _requests;

    /// <summary>Answers requests to <paramref name="path"/> with the body of a recorded fixture.</summary>
    public RoutedJiraHandler Fixture(string path, string fixture, HttpStatusCode status = HttpStatusCode.OK, Task? gate = null)
    {
        _routes[path] = new Route(status, RecordedFixtures.Read(fixture), gate);

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Enqueue(request.RequestUri!);

        if (!_routes.TryGetValue(request.RequestUri!.AbsolutePath, out var route))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (route.Gate is not null)
        {
            await route.Gate.WaitAsync(cancellationToken);
        }

        return new HttpResponseMessage(route.Status)
        {
            Content = new StringContent(route.Body, Encoding.UTF8, "application/json"),
        };
    }

    private sealed record Route(HttpStatusCode Status, string Body, Task? Gate);
}
