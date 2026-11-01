namespace Geeratya.Jira.Transport;

/// <summary>Parses the <c>GEERATYA_LIVE_HOSTS</c> allowlist.</summary>
public static class JiraHostAllowList
{
    public const string EnvironmentVariable = "GEERATYA_LIVE_HOSTS";

    /// <summary>Splits a comma-separated host list. Empty or missing gives an empty set (nothing allowed).</summary>
    public static IReadOnlySet<string> Parse(string? commaSeparatedHosts)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(commaSeparatedHosts))
        {
            return hosts;
        }

        var options = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

        foreach (var host in commaSeparatedHosts.Split(',', options))
        {
            hosts.Add(host);
        }

        return hosts;
    }
}
