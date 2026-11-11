using System.Net;
using System.Text.Json.Serialization.Metadata;

namespace Geeratya.Jira.Transport;

public sealed partial class JiraTransport
{
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

            // A rate-limit wait, or any wait long enough to be seen (a 5xx or network failure with a Retry-After),
            // is published for the banner. The plain exponential back-off is at most 2 s (0.5 s doubling, three
            // retries), shorter than one banner tick, so it stays unpublished.
            if (result.Error.Kind == JiraErrorKind.RateLimited || wait > _visibleWait)
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
}
