using System.Collections.Concurrent;
using System.Net;

namespace Geeratya.Jira.Tests.Fakes;

/// <summary>
/// In-process fake Jira: serves scripted replies by method and path, records every request and can hang, fail the
/// connection or hold a reply behind a gate. Never opens a socket. A path with no script gets 404.
/// </summary>
public sealed class FakeJiraHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<FakeResponse>> _scripts = new();
    private readonly ConcurrentDictionary<string, FakeResponse> _defaults = new();
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private readonly object _flightLock = new();
    private readonly List<(int Count, TaskCompletionSource Signal)> _flightWaiters = [];
    private int _inFlight;
    private int _peakInFlight;

    public IReadOnlyCollection<RecordedRequest> Requests => _requests;

    public int PeakInFlight
    {
        get
        {
            lock (_flightLock)
            {
                return _peakInFlight;
            }
        }
    }

    /// <summary>Serves this reply every time the route is hit and no one-off reply is queued.</summary>
    public FakeJiraHandler Serve(HttpMethod method, string path, FakeResponse response)
    {
        _defaults[Key(method, path)] = response;

        return this;
    }

    /// <summary>Queues one-off replies, used in order before the default.</summary>
    public FakeJiraHandler Script(HttpMethod method, string path, params FakeResponse[] responses)
    {
        var queue = _scripts.GetOrAdd(Key(method, path), _ => new ConcurrentQueue<FakeResponse>());

        foreach (var response in responses)
        {
            queue.Enqueue(response);
        }

        return this;
    }

    /// <summary>Completes when at least <paramref name="count"/> requests are being served at the same time.</summary>
    public Task WaitForInFlightAsync(int count)
    {
        lock (_flightLock)
        {
            if (_peakInFlight >= count)
            {
                return Task.CompletedTask;
            }

            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _flightWaiters.Add((count, signal));

            return signal.Task;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Enqueue(Record(request, body));
        EnterFlight();

        try
        {
            var response = Next(request);

            return await Answer(response, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static string Key(HttpMethod method, string path) => $"{method.Method} {path}";

    private static RecordedRequest Record(HttpRequestMessage request, string? body) =>
        new(
            request.Method,
            request.RequestUri!.PathAndQuery,
            request.RequestUri.Host,
            request.Headers.Authorization?.ToString(),
            request.Headers.Accept.ToString(),
            request.Headers.UserAgent.ToString(),
            body);

    private static async Task<HttpResponseMessage> Answer(FakeResponse response, CancellationToken cancellationToken)
    {
        if (response.NetworkFailure)
        {
            throw new HttpRequestException(HttpRequestError.ConnectionError, "Simulated connection failure.");
        }

        if (response.Unresponsive)
        {
            // Deliberately not given the token: the caller's own deadline is the only way out.
            await new TaskCompletionSource().Task;
        }

        if (response.Hang)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (response.Gate is not null)
        {
            await response.Gate.WaitAsync(cancellationToken);
        }

        var message = new HttpResponseMessage(response.Status)
        {
            Content = response.StalledBody ? new StalledContent() : new StringContent(response.Body),
        };

        foreach (var (name, value) in response.Headers ?? new Dictionary<string, string>())
        {
            message.Headers.TryAddWithoutValidation(name, value);
        }

        return message;
    }

    private FakeResponse Next(HttpRequestMessage request)
    {
        var key = Key(request.Method, request.RequestUri!.AbsolutePath);

        if (_scripts.TryGetValue(key, out var queue) && queue.TryDequeue(out var scripted))
        {
            return scripted;
        }

        return _defaults.GetValueOrDefault(key) ?? FakeResponse.WithStatus(HttpStatusCode.NotFound);
    }

    private void EnterFlight()
    {
        var current = Interlocked.Increment(ref _inFlight);

        lock (_flightLock)
        {
            _peakInFlight = Math.Max(_peakInFlight, current);

            foreach (var waiter in _flightWaiters.Where(entry => entry.Count <= _peakInFlight).ToList())
            {
                waiter.Signal.TrySetResult();
                _flightWaiters.Remove(waiter);
            }
        }
    }
}

/// <summary>A body that never arrives and whose read does not look at the cancellation token.</summary>
internal sealed class StalledContent : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        new TaskCompletionSource().Task;

    protected override bool TryComputeLength(out long length)
    {
        length = -1;

        return false;
    }
}
