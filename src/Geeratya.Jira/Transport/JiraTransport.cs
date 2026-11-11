using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Geeratya.Jira.Transport;

/// <summary>
/// The only sender of Jira requests: one instance, one <see cref="HttpClient"/> and one concurrency cap per
/// connection. It owns auth, headers, timeouts, retries of idempotent requests, <c>Retry-After</c> handling and the
/// mapping of every failure to a <see cref="JiraError"/>. The API token lives only here.
/// </summary>
public sealed class JiraTransport : IDisposable
{
    private const string JsonMediaType = "application/json";
    private const int MaxBackoffExponent = 10;
    private const string AbandonedMessage = "Abandoned for a user request; no reply from Jira in time.";

    private readonly JiraConnection _connection;
    private readonly string _authorization;
    private readonly string _token;
    private readonly string _siteBase;
    private readonly JiraTransportOptions _options;
    private readonly HttpClient _client;
    private readonly RequestGate _gate;
    private readonly RateLimitWaitTracker _rateWaits = new();
    private readonly object _rateLock = new();
    private JiraRateLimit? _rateLimit;
    private DateTimeOffset _rateWindowEnds;

    /// <param name="connection">The site and account this transport talks to.</param>
    /// <param name="token">The API token for the account.</param>
    /// <param name="handler">The message handler; a fake in tests. Disposed with the transport.</param>
    /// <param name="options">Timeouts, retries and the host allowlist.</param>
    public JiraTransport(
        JiraConnection connection,
        JiraApiToken token,
        HttpMessageHandler? handler = null,
        JiraTransportOptions? options = null)
    {
        _connection = connection;
        RequireSecureSite(connection.SiteUrl);
        _options = options ?? new JiraTransportOptions();
        _token = token.Value;
        _siteBase = connection.SiteUrl.AbsoluteUri.TrimEnd('/');

        _authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connection.AccountEmail}:{_token}"));
        _client = new HttpClient(handler ?? CreateDefaultHandler(), true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _gate = new RequestGate(_options.MaxConcurrentRequests, _options.ReservedForUser);
    }

    public JiraConnection Connection => _connection;

    /// <summary>
    /// When a request is waiting for the Jira rate limit (an exhausted budget or a <c>Retry-After</c>), the latest
    /// time such a wait ends; null when nothing waits. Poll it from the UI loop.
    /// </summary>
    public DateTimeOffset? RateLimitedUntil => _rateWaits.Until;

    /// <summary>
    /// Raised when a rate-limit wait starts or ends, so the UI can redraw. It runs on whichever thread changed the
    /// state (not the UI thread); handlers must only post to the UI, never block.
    /// </summary>
    public event Action RateLimitWaitChanged
    {
        add => _rateWaits.Changed += value;
        remove => _rateWaits.Changed -= value;
    }

    public Task<JiraResult<T>> SendAsync<T>(
        JiraRequest request,
        JsonTypeInfo<T> resultInfo,
        CancellationToken cancellationToken) =>
        SendCoreAsync(request, resultInfo, cancellationToken);

    /// <summary>
    /// Sends a request without the read/write check; the entry point for the write extensions in
    /// <c>Geeratya.Jira.Writes</c>, which add the typed write result on top.
    /// </summary>
    internal Task<JiraResult<T>> SendUncheckedAsync<T>(
        JiraRequest request,
        JsonTypeInfo<T>? resultInfo,
        CancellationToken cancellationToken) =>
        SendCoreAsync(request, resultInfo, cancellationToken);

    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task<JiraResult<T>> SendCoreAsync<T>(
        JiraRequest request,
        JsonTypeInfo<T>? resultInfo,
        CancellationToken cancellationToken)
    {
        if (!IsHostAllowed())
        {
            var message = $"Host {_connection.SiteUrl.Host} is not in {JiraHostAllowList.EnvironmentVariable}.";

            return JiraResults.Failure<T>(new JiraError(JiraErrorKind.HostRefused, null, message));
        }

        using var operation = new OperationDeadline(_options.OperationDeadline, _options.Clock, cancellationToken);

        try
        {
            return await RunAttemptsAsync(request, resultInfo, operation, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OperationTimedOut<T>();
        }
    }

    private async Task<JiraResult<T>> RunAttemptsAsync<T>(
        JiraRequest request,
        JsonTypeInfo<T>? resultInfo,
        OperationDeadline operation,
        CancellationToken callerToken)
    {
        var attempt = 0;

        while (true)
        {
            var result = await AttemptAsync(request, resultInfo, operation, callerToken);

            if (result.IsSuccess || operation.Token.IsCancellationRequested)
            {
                return result;
            }

            if (result.Error.Message != AbandonedMessage && !ShouldRetry(request, result.Error, attempt))
            {
                return result;
            }

            // Dropped by the transport itself for a user request: asked again behind it, without spending a retry
            // (the operation deadline still bounds it), so a healthy link never shows a refresh as failed.
            if (result.Error.Message == AbandonedMessage)
            {
                continue;
            }

            var wait = WaitBeforeRetry(result.Error, attempt);

            if (result.Error.Kind == JiraErrorKind.RateLimited)
            {
                await WaitForRateLimitAsync(wait, operation);
            }
            else
            {
                await _options.Delay(wait, operation.Token);
            }

            attempt++;
        }
    }

    private JiraResult<T> OperationTimedOut<T>() =>
        Fail<T>(JiraErrorKind.Timeout, $"Jira did not finish within {_options.OperationDeadline.TotalSeconds:0} s.");

    /// <summary>The handler a transport uses when none is given; public so tests can inspect its settings.</summary>
    public static SocketsHttpHandler CreateDefaultHandler() =>
        new()
        {
            // The system proxy (HTTPS_PROXY, WinHTTP settings) with the signed-in user's credentials, as the update
            // client does: an NTLM or Kerberos corporate proxy otherwise answers every call with 407.
            Proxy = HttpClient.DefaultProxy,
            DefaultProxyCredentials = CredentialCache.DefaultCredentials,
            // Redirects could carry the credential to another host; a 3xx is reported as an unexpected status.
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,

            // Reduces the chance of reusing a connection that died while the app sat idle: connections are retired
            // early and HTTP/2 pings (requests ask for HTTP/2 or lower) detect a dead one. The WaitAsync deadline is what guarantees the request ends.
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
            KeepAlivePingDelay = TimeSpan.FromSeconds(30),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(15),
        };

    private static void RequireSecureSite(Uri siteUrl)
    {
        if (siteUrl.Scheme == Uri.UriSchemeHttps || siteUrl.IsLoopback)
        {
            return;
        }

        throw new ArgumentException("The site URL must use https (http only for loopback).", nameof(siteUrl));
    }

    private bool IsHostAllowed() =>
        _options.AllowedHosts is null || _options.AllowedHosts.Contains(_connection.SiteUrl.Host);

    private bool ShouldRetry(JiraRequest request, JiraError error, int attempt)
    {
        if (attempt >= _options.MaxRetries)
        {
            return false;
        }

        // A wait beyond the cap would only end in another 429 or 503; return it at once, the caller reads RetryAfter.
        if (error.RetryAfter > _options.MaxWait)
        {
            return false;
        }

        // A timeout is the signature of a pooled connection that died while the app sat idle (sleep, VPN or NAT
        // change). One more try usually lands on a new connection; a second timeout is real. Only reads: a write
        // may have landed.
        if (error.Kind == JiraErrorKind.Timeout)
        {
            return request.IsIdempotent && attempt == 0;
        }

        if (!request.IsIdempotent)
        {
            // A 429 means Jira did not process the write; without a Retry-After the wait is a guess, so give up.
            return error.Kind == JiraErrorKind.RateLimited && error.RetryAfter is not null;
        }

        return error.Kind switch
        {
            JiraErrorKind.RateLimited or JiraErrorKind.Network => true,
            JiraErrorKind.ServerError => error.StatusCode != (int)HttpStatusCode.NotImplemented,
            _ => false,
        };
    }

    private TimeSpan WaitBeforeRetry(JiraError error, int attempt)
    {
        if (error.RetryAfter is { } retryAfter)
        {
            return retryAfter;
        }

        var factor = Math.Pow(2, Math.Min(attempt, MaxBackoffExponent));
        var jittered = _options.BaseBackoff * factor * (0.5 + (_options.Jitter() * 0.5));

        return Min(jittered, _options.MaxWait);
    }

    private async Task<JiraResult<T>> AttemptAsync<T>(
        JiraRequest request,
        JsonTypeInfo<T>? resultInfo,
        OperationDeadline operation,
        CancellationToken callerToken)
    {
        await ThrottleAsync(request.Priority, operation);
        var lease = await AcquireSlotAsync(request.Priority, operation.Token);

        try
        {
            return await SendOnceAsync(request, resultInfo, lease, operation.Token, callerToken);
        }
        finally
        {
            _gate.Release(lease);
        }
    }

    /// <summary>
    /// Takes a slot. A user request that waits longer than <see cref="JiraTransportOptions.PreemptBackgroundAfter"/>
    /// abandons the oldest background request holding one, and again every period while it still waits.
    /// </summary>
    private async Task<RequestLease> AcquireSlotAsync(JiraPriority priority, CancellationToken cancellationToken)
    {
        var acquire = _gate.AcquireAsync(priority, cancellationToken);

        if (priority != JiraPriority.User || acquire.IsCompleted)
        {
            return await acquire;
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            while (true)
            {
                var tick = Task.Delay(_options.PreemptBackgroundAfter, _options.Clock, stop.Token);

                if (await Task.WhenAny(acquire, tick) == acquire)
                {
                    return await acquire;
                }

                _gate.AbandonOldestBackground();
            }
        }
        finally
        {
            await stop.CancelAsync();
        }
    }

    private async Task<JiraResult<T>> SendOnceAsync<T>(
        JiraRequest request,
        JsonTypeInfo<T>? resultInfo,
        RequestLease lease,
        CancellationToken operationToken,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(operationToken, lease.AbandonToken);
        timeout.CancelAfter(_options.Timeout);

        try
        {
            using var message = CreateMessage(request);

            // WaitAsync is the hard deadline: a connection that went dead while the app sat idle (a laptop asleep, a
            // NAT that dropped the flow) can leave a socket call that never honours the token, and the request would
            // then wait for ever. The abandoned call ends with the handler or the connection; nothing waits for it.
            using var response = await _client.SendAsync(message, timeout.Token).WaitAsync(timeout.Token);
            RecordRateLimit(response);
            var body = await response.Content.ReadAsStringAsync(timeout.Token).WaitAsync(timeout.Token);

            return response.IsSuccessStatusCode
                ? ParseBody(body, resultInfo, request.AllowsEmptyBody)
                : JiraResults.Failure<T>(MapFailure(response, body));
        }
        catch (OperationCanceledException) when (lease.IsAbandoned && !cancellationToken.IsCancellationRequested)
        {
            return Fail<T>(JiraErrorKind.Timeout, AbandonedMessage);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            return OperationTimedOut<T>();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail<T>(JiraErrorKind.Timeout, $"No reply from Jira within {_options.Timeout.TotalSeconds:0} s.");
        }
        catch (HttpRequestException exception) when (JiraErrorMapper.IsProxyAuthentication(exception))
        {
            // Not retried: the same credentials would get the same 407.
            return JiraResults.Failure<T>(JiraErrorMapper.ProxyAuthenticationRequired());
        }
        catch (HttpRequestException exception)
        {
            return Fail<T>(JiraErrorKind.Network, $"Network failure ({exception.HttpRequestError}).");
        }
    }

    /// <summary>Waits out the rate-limit window when the last response said the budget is (nearly) spent.</summary>
    private async Task ThrottleAsync(JiraPriority priority, OperationDeadline operation)
    {
        var threshold = priority == JiraPriority.User ? 0 : _options.BackgroundThrottleRemaining;
        TimeSpan wait;

        lock (_rateLock)
        {
            if (_rateLimit is not { } limit || limit.Remaining > threshold)
            {
                return;
            }

            wait = _rateWindowEnds - _options.Clock.GetUtcNow();

            if (wait <= TimeSpan.Zero)
            {
                _rateLimit = null;

                return;
            }
        }

        await WaitForRateLimitAsync(Min(wait, _options.MaxWait), operation);

        lock (_rateLock)
        {
            // The window has passed; the next response tells the new budget.
            if (_rateLimit is { } current && current.Remaining <= threshold)
            {
                _rateLimit = null;
            }
        }
    }

    private Task WaitForRateLimitAsync(TimeSpan wait, OperationDeadline operation)
    {
        if (wait <= TimeSpan.Zero)
        {
            return _options.Delay(wait, operation.Token);
        }

        // The wait is Jira's request, not a hang: it does not count against the operation deadline.
        operation.Extend(wait);

        return _rateWaits.RunAsync(_options.Clock.GetUtcNow() + wait, () => _options.Delay(wait, operation.Token));
    }

    private void RecordRateLimit(HttpResponseMessage response)
    {
        // A 429 is answered by the Retry-After wait; recording its (empty) budget would wait a second time.
        if (response.StatusCode == HttpStatusCode.TooManyRequests
            || JiraRateLimit.Read(response.Headers) is not { } limit)
        {
            return;
        }

        lock (_rateLock)
        {
            var now = _options.Clock.GetUtcNow();

            // Replies can finish out of order: inside a live window the lower budget stays.
            if (_rateLimit is { } known && now < _rateWindowEnds && known.Remaining < limit.Remaining)
            {
                return;
            }

            _rateLimit = limit;
            _rateWindowEnds = now + limit.Reset;
        }
    }

    private HttpRequestMessage CreateMessage(JiraRequest request)
    {
        var message = new HttpRequestMessage(request.Method, $"{_siteBase}{request.Path}")
        {
            // Ask for HTTP/2 (Jira Cloud offers it) so the handler's keep-alive pings run; plain HTTP/1.1 has none.
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", _authorization);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
        message.Headers.UserAgent.ParseAdd(_options.UserAgent);

        if (request.JsonBody is not null)
        {
            message.Content = new StringContent(request.JsonBody, Encoding.UTF8, JsonMediaType);
        }

        return message;
    }

    private JiraResult<T> ParseBody<T>(string body, JsonTypeInfo<T>? resultInfo, bool allowsEmptyBody)
    {
        if (resultInfo is null || (allowsEmptyBody && string.IsNullOrWhiteSpace(body)))
        {
            return JiraResults.Success<T>(default!);
        }

        try
        {
            var value = JsonSerializer.Deserialize(body, resultInfo);

            return value is null
                ? Fail<T>(JiraErrorKind.InvalidResponse, "Jira returned an empty response.")
                : JiraResults.Success(value);
        }
        catch (JsonException)
        {
            return Fail<T>(JiraErrorKind.InvalidResponse, "Jira returned a response that could not be read.");
        }
    }

    private JiraError MapFailure(HttpResponseMessage response, string body)
    {
        var error = JiraErrorMapper.Map(response.StatusCode, body, ReadRetryAfter(response));

        return error with
        {
            Message = Redact(error.Message),
            FieldErrors = error.FieldErrors?.ToDictionary(pair => pair.Key, pair => Redact(pair.Value)),
            Messages = error.Messages?.Select(Redact).ToList(),
        };
    }

    private TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;

        if (header?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - _options.Clock.GetUtcNow();

            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    private JiraResult<T> Fail<T>(JiraErrorKind kind, string message) =>
        JiraResults.Failure<T>(new JiraError(kind, null, Redact(message)));

    private string Redact(string text) =>
        text.Replace(_token, JiraApiToken.RedactedText, StringComparison.Ordinal)
            .Replace(_authorization, JiraApiToken.RedactedText, StringComparison.Ordinal);

    private static TimeSpan Min(TimeSpan first, TimeSpan second) => first < second ? first : second;
}
