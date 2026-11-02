using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace Geeratya.FakeJira;

/// <summary>Local HTTP server on 127.0.0.1 that answers like Jira from a <see cref="FakeJiraRouter"/>.</summary>
public sealed class FakeJiraServer : IDisposable
{
    private const string Host = "127.0.0.1";
    private const int RetryAfterSeconds = 1;
    private const int TooManyRequests = 429;
    private const int ServerError = 500;
    private const int StartAttempts = 10;
    private const string RetryAfterHeader = "Retry-After";

    private readonly FakeJiraOptions _options;
    private readonly FakeJiraRouter _router;
    private readonly CancellationTokenSource _stop = new();
    private HttpListener? _listener;
    private int _requestCount;
    private int _writeCount;

    public FakeJiraServer(FakeJiraOptions options)
    {
        _options = options;
        _router = new FakeJiraRouter(options);
    }

    /// <summary>Starts listening and returns the base URL; serves on background tasks.</summary>
    public string Start()
    {
        for (var attempt = 0; attempt < StartAttempts; attempt++)
        {
            var port = _options.Port != 0 ? _options.Port : FreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://{Host}:{port}/");

            try
            {
                listener.Start();
            }
            catch (HttpListenerException) when (_options.Port == 0)
            {
                listener.Close();

                continue;
            }

            _listener = listener;
            _ = Task.Run(AcceptLoopAsync);

            return $"http://{Host}:{port}";
        }

        throw new InvalidOperationException("No free port found.");
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener?.Close();
        _stop.Dispose();
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        return port;
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested && _listener is { IsListening: true })
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            if (_options.LatencyMilliseconds > 0)
            {
                await Task.Delay(_options.LatencyMilliseconds, _stop.Token);
            }

            var reply = Reply(context.Request);
            var response = context.Response;

            if (reply.DropConnection)
            {
                await DropAsync(response);

                return;
            }

            response.StatusCode = reply.Status;
            response.ContentType = "application/json;charset=UTF-8";

            if (reply.RetryAfterSeconds is { } seconds)
            {
                response.AddHeader(RetryAfterHeader, seconds.ToString());
            }

            response.ContentLength64 = reply.Body.Length;
            await response.OutputStream.WriteAsync(reply.Body, _stop.Token);
            response.Close();
        }
        catch (Exception exception) when (exception is HttpListenerException or OperationCanceledException
            or ObjectDisposedException)
        {
            // The client went away or the server is stopping.
        }
        catch (Exception exception)
        {
            AnswerServerError(context, exception);
        }
    }

    /// <summary>
    /// Promises a body, sends the headers and cuts the connection, so the client fails reading the reply:
    /// HttpListener cannot close a connection before any reply bytes.
    /// </summary>
    private async Task DropAsync(HttpListenerResponse response)
    {
        response.ContentLength64 = 1;
        await response.OutputStream.FlushAsync(_stop.Token);
        response.Abort();
    }

    private static void AnswerServerError(HttpListenerContext context, Exception exception)
    {
        try
        {
            var text = $"{{\"errorMessages\":[\"Fake Jira failed: {exception.GetType().Name}\"]}}";
            var body = Encoding.UTF8.GetBytes(text);
            context.Response.StatusCode = ServerError;
            context.Response.ContentLength64 = body.Length;
            context.Response.OutputStream.Write(body);
            context.Response.Close();
        }
        catch (Exception closing) when (closing is HttpListenerException or ObjectDisposedException)
        {
            // The client is gone as well.
        }
    }

    private FakeReply Reply(HttpListenerRequest request)
    {
        var count = Interlocked.Increment(ref _requestCount);

        if (_options.RateLimitEvery > 0 && count % _options.RateLimitEvery == 0)
        {
            var rateLimitBody = "{\"message\":\"Rate limit exceeded.\"}"u8.ToArray();

            return new FakeReply(TooManyRequests, rateLimitBody, RetryAfterSeconds);
        }

        var query = new Dictionary<string, string>();
        var parsed = HttpUtility.ParseQueryString(request.Url?.Query ?? string.Empty);

        foreach (var key in parsed.AllKeys)
        {
            if (key is not null)
            {
                query[key] = parsed[key] ?? string.Empty;
            }
        }

        var text = string.Empty;

        if (request.HasEntityBody)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            text = reader.ReadToEnd();
        }

        var routed = _router.Route(request.HttpMethod, request.Url?.AbsolutePath ?? "/", query, text);

        if (_options.DropWriteEvery > 0 && FakeJiraRouter.IsWrite(request.HttpMethod)
            && Interlocked.Increment(ref _writeCount) % _options.DropWriteEvery == 0)
        {
            return routed with { DropConnection = true };
        }

        return routed;
    }
}
