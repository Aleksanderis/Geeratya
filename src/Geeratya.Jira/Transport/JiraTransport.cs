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

    private readonly JiraConnection _connection;
    private readonly string _authorization;
    private readonly string _token;
    private readonly string _siteBase;
    private readonly JiraTransportOptions _options;
    private readonly HttpClient _client;
    private readonly SemaphoreSlim _concurrency;

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
        _options = options ?? new JiraTransportOptions();
        _token = token.Value;
        _siteBase = connection.SiteUrl.AbsoluteUri.TrimEnd('/');

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connection.AccountEmail}:{_token}"));
        _authorization = credentials;
        _client = new HttpClient(handler ?? new SocketsHttpHandler(), true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _concurrency = new SemaphoreSlim(_options.MaxConcurrentRequests, _options.MaxConcurrentRequests);
    }

    public JiraConnection Connection => _connection;

    public async Task<JiraResult<T>> SendAsync<T>(
        JiraRequest request,
        JsonTypeInfo<T> resultInfo,
        CancellationToken cancellationToken)
    {
        if (!IsHostAllowed())
        {
            var message = $"Host {_connection.SiteUrl.Host} is not in {JiraHostAllowList.EnvironmentVariable}.";

            return JiraResults.Failure<T>(new JiraError(JiraErrorKind.HostRefused, null, message));
        }

        var attempt = 0;

        while (true)
        {
            var result = await AttemptAsync(request, resultInfo, cancellationToken);

            if (result.IsSuccess || !ShouldRetry(request, result.Error, attempt))
            {
                return result;
            }

            await _options.Delay(WaitBeforeRetry(result.Error, attempt), cancellationToken);
            attempt++;
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _concurrency.Dispose();
    }

    private bool IsHostAllowed() =>
        _options.AllowedHosts is null || _options.AllowedHosts.Contains(_connection.SiteUrl.Host);

    private bool ShouldRetry(JiraRequest request, JiraError error, int attempt)
    {
        if (!request.IsIdempotent || attempt >= _options.MaxRetries)
        {
            return false;
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
            return Min(retryAfter, _options.MaxWait);
        }

        var factor = Math.Pow(2, Math.Min(attempt, MaxBackoffExponent));
        var jittered = _options.BaseBackoff * factor * (0.5 + (_options.Jitter() * 0.5));

        return Min(jittered, _options.MaxWait);
    }

    private async Task<JiraResult<T>> AttemptAsync<T>(
        JiraRequest request,
        JsonTypeInfo<T> resultInfo,
        CancellationToken cancellationToken)
    {
        await _concurrency.WaitAsync(cancellationToken);

        try
        {
            return await SendOnceAsync(request, resultInfo, cancellationToken);
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private async Task<JiraResult<T>> SendOnceAsync<T>(
        JiraRequest request,
        JsonTypeInfo<T> resultInfo,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        try
        {
            using var message = CreateMessage(request);
            using var response = await _client.SendAsync(message, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            return response.IsSuccessStatusCode
                ? ParseBody(body, resultInfo)
                : JiraResults.Failure<T>(MapFailure(response, body));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail<T>(JiraErrorKind.Timeout, $"No reply from Jira within {_options.Timeout.TotalSeconds:0} s.");
        }
        catch (HttpRequestException exception)
        {
            return Fail<T>(JiraErrorKind.Network, $"Network failure ({exception.HttpRequestError}).");
        }
    }

    private HttpRequestMessage CreateMessage(JiraRequest request)
    {
        var message = new HttpRequestMessage(request.Method, $"{_siteBase}{request.Path}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", _authorization);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
        message.Headers.UserAgent.ParseAdd(_options.UserAgent);

        if (request.JsonBody is not null)
        {
            message.Content = new StringContent(request.JsonBody, Encoding.UTF8, JsonMediaType);
        }

        return message;
    }

    private JiraResult<T> ParseBody<T>(string body, JsonTypeInfo<T> resultInfo)
    {
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

        return error with { Message = Redact(error.Message) };
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
