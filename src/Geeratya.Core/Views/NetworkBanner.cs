using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Views;

/// <summary>
/// The words of the network states: the one-line banner under the tab bar and the word in the status line. Pure: the
/// time arrives as a parameter. Every state has a text marker, so the no-colour tier never relies on colour
/// (<c>docs/ux-screens.md</c>, "Connection check and network states").
/// </summary>
public static class NetworkBanner
{
    /// <summary>Terminals narrower than this get the short wording.</summary>
    public const int CompactBelow = 60;

    private const string OfflineBadge = " OFFLINE ";
    private const string RateLimitedBadge = " RATE LIMITED ";
    private const string StaleBadge = " STALE ";
    private const string ErrorBadge = " ERROR ";
    private const string RetryNow = "<space>r retries now.";
    private const string RefreshHint = "<space>r refreshes.";
    private const string Gap = " ";

    /// <summary>
    /// The banner line of the active tab, or null when its status needs none (fresh, or loading, which only the
    /// status line mentions).
    /// </summary>
    public static StyledLine? Line(AppState state, DateTimeOffset now, int width)
    {
        var session = state.Session;
        var tab = session.Active is { } id ? session.Tabs.FirstOrDefault(candidate => candidate.Id == id) : null;

        if (tab is null)
        {
            return null;
        }

        var status = TabStatus.Of(tab, session.Link, now);
        var compact = width < CompactBelow;

        return status.Kind switch
        {
            NetworkStatusKind.Offline => Banner(OfflineBadge, TextRole.Offline, OfflineText(tab, session, now, compact)),
            NetworkStatusKind.RateLimited => Banner(
                RateLimitedBadge,
                TextRole.RateLimited,
                RateLimitedText(status.Until!.Value, now, compact)),
            NetworkStatusKind.Stale => Banner(StaleBadge, TextRole.Stale, StaleText(tab, now, compact)),
            NetworkStatusKind.Error => Banner(ErrorBadge, TextRole.Error, ErrorText(status, session, compact)),
            _ => null,
        };
    }

    /// <summary>
    /// The word the status line shows for the active tab (<c>offline</c>, <c>rate limited 25s</c>, <c>stale</c>,
    /// <c>error</c>, <c>loading</c>), or empty when there is nothing to report. It stays visible when the banner is cut.
    /// </summary>
    public static string StatusWord(TabStatus status, DateTimeOffset now)
    {
        return status.Kind switch
        {
            NetworkStatusKind.Loading => "loading",
            NetworkStatusKind.Stale => "stale",
            NetworkStatusKind.Offline => "offline",
            NetworkStatusKind.RateLimited => $"rate limited {Seconds(status.Until!.Value - now)}s",
            NetworkStatusKind.Error => "error",
            _ => string.Empty,
        };
    }

    /// <summary>The age of data as <c>45 s</c>, <c>23 min</c>, <c>5 h</c> or <c>3 d</c>.</summary>
    public static string Age(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return $"{Math.Max(0, (int)age.TotalSeconds)} s";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes} min";
        }

        return age < TimeSpan.FromDays(2) ? $"{(int)age.TotalHours} h" : $"{(int)age.TotalDays} d";
    }

    private static StyledLine Banner(string badge, TextRole role, string text)
    {
        return new StyledLine([new StyledSpan(badge, role), new StyledSpan($"{Gap}{text}", TextRole.Body)]);
    }

    private static string OfflineText(TabState tab, SiteSession session, DateTimeOffset now, bool compact)
    {
        var data = DataAge(tab, now);

        if (compact)
        {
            return data.Length > 0 ? $"Data {data}" : "Cannot reach Jira";
        }

        var host = HostOf(session.SiteUrl);
        var reason = tab.Load is Failed { Kind: JiraErrorKind.Timeout }
            ? $"{host} did not answer in time."
            : $"Cannot reach {host}.";
        var dataClause = data.Length > 0 ? $"{Gap}Data is {data}." : string.Empty;

        // The part to act on comes before the age, so a cut line loses the age first.
        return $"{reason}{Gap}{RetryClause(tab, now)}{dataClause}";
    }

    private static string DataAge(TabState tab, DateTimeOffset now)
    {
        if (tab.Rows.Count == 0 || tab.LoadedAt is not { } loaded)
        {
            return string.Empty;
        }

        return $"{Age(now - loaded)} old";
    }

    private static string RetryClause(TabState tab, DateTimeOffset now)
    {
        if (tab.Load is Loading)
        {
            return "Retrying now.";
        }

        if (tab.NextRefreshAt is { } due && due > now)
        {
            return $"Retrying in {Seconds(due - now)} s, {RetryNow}";
        }

        return RetryNow;
    }

    private static string RateLimitedText(DateTimeOffset until, DateTimeOffset now, bool compact)
    {
        var wait = Seconds(until - now);

        return compact ? $"Retrying in {wait} s" : $"Waiting for the Jira rate limit. Retrying in {wait} s.";
    }

    private static string StaleText(TabState tab, DateTimeOffset now, bool compact)
    {
        var shownAt = tab.Detail is [.., { IssueStale: true } top] ? top.LoadedAt : tab.LoadedAt;
        var age = shownAt is { } loaded ? $"{Age(now - loaded)} old" : string.Empty;
        var refreshing = tab.Load is Loading || tab.Detail is [.., { IssueStale: true }];

        if (compact)
        {
            return StaleCompact(age, refreshing);
        }

        var data = age.Length > 0 ? $"Data is {age}." : "Data may be old.";

        return $"{data}{Gap}{(refreshing ? "Refreshing…" : RefreshHint)}";
    }

    private static string StaleCompact(string age, bool refreshing)
    {
        var head = age.Length > 0 ? age : "old data";

        return refreshing ? $"{head}, refreshing…" : head;
    }

    private static string ErrorText(TabStatus status, SiteSession session, bool compact)
    {
        if (status.CredentialsRejected)
        {
            return compact ? "Token rejected. See :profile" : RejectedText(session);
        }

        return status.Failure is { } failure ? failure.Message : string.Empty;
    }

    private static string RejectedText(SiteSession session)
    {
        var name = session.ProfileName ?? "NAME";

        return $"Jira rejected the token (401). Replace it: :profile remove {name}, then :profile add.";
    }

    private static int Seconds(TimeSpan span)
    {
        return Math.Max(1, (int)Math.Ceiling(span.TotalSeconds));
    }

    private static string HostOf(string? siteUrl)
    {
        return Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri) ? uri.Host : "the site";
    }
}
