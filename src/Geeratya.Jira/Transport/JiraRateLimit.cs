using System.Globalization;
using System.Net.Http.Headers;

namespace Geeratya.Jira.Transport;

/// <summary>What the rate-limit headers of one response say: requests left and time until the window resets.</summary>
internal readonly record struct JiraRateLimit(int Remaining, TimeSpan Reset)
{
    public const string RateLimitHeader = "ratelimit";
    public const string RemainingHeader = "x-ratelimit-remaining";
    private const string RemainingToken = "r=";
    private const string ResetToken = "t=";

    private static readonly TimeSpan _defaultReset = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Reads <c>ratelimit: "jira-burst-based";r=&lt;remaining&gt;;t=&lt;reset seconds&gt;</c> (with several
    /// comma-separated policies the tightest wins), else <c>x-ratelimit-remaining</c> with a one-second window (the
    /// burst window). Returns null when neither is present or readable.
    /// </summary>
    public static JiraRateLimit? Read(HttpResponseHeaders headers)
    {
        if (headers.TryGetValues(RateLimitHeader, out var values) && ParsePolicies(values) is { } policy)
        {
            return policy;
        }

        if (headers.TryGetValues(RemainingHeader, out var remaining)
            && int.TryParse(remaining.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var left))
        {
            return new JiraRateLimit(left, _defaultReset);
        }

        return null;
    }

    private static JiraRateLimit? ParsePolicies(IEnumerable<string> values)
    {
        JiraRateLimit? tightest = null;

        foreach (var policy in values.SelectMany(value => value.Split(',')))
        {
            int? remaining = null;
            var reset = _defaultReset;

            foreach (var part in policy.Split(';'))
            {
                var pair = part.Trim();

                if (pair.StartsWith(RemainingToken, StringComparison.Ordinal)
                    && int.TryParse(pair[RemainingToken.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var left))
                {
                    remaining = left;
                }
                else if (pair.StartsWith(ResetToken, StringComparison.Ordinal)
                    && double.TryParse(pair[RemainingToken.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                    && seconds >= 0)
                {
                    reset = TimeSpan.FromSeconds(seconds);
                }
            }

            if (remaining is { } r && (tightest is null || r < tightest.Value.Remaining))
            {
                tightest = new JiraRateLimit(r, reset);
            }
        }

        return tightest;
    }
}
