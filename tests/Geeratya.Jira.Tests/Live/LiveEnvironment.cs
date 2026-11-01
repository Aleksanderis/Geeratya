using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Live;

/// <summary>The dev-site settings of live tests, read from the <c>GEERATYA_LIVE_*</c> variables.</summary>
public sealed class LiveEnvironment
{
    public const string EmailVariable = "GEERATYA_LIVE_EMAIL";
    public const string TokenVariable = "GEERATYA_LIVE_TOKEN";

    private LiveEnvironment(JiraConnection connection, string token)
    {
        Connection = connection;
        Token = token;
    }

    public JiraConnection Connection { get; }

    public string Token { get; }

    /// <summary>Returns why live tests cannot run, or null when every variable is set.</summary>
    public static string? MissingReason(Func<string, string?> lookup)
    {
        var missing = new[] { JiraHostAllowList.EnvironmentVariable, EmailVariable, TokenVariable }
            .Where(name => string.IsNullOrWhiteSpace(lookup(name)))
            .ToArray();

        if (missing.Length == 0)
        {
            return null;
        }

        return $"Live test skipped: set {string.Join(", ", missing)} (see the testing skill, \"Live dev site\").";
    }

    /// <summary>The dev site is the first host of the allowlist; the transport refuses every other host.</summary>
    public static LiveEnvironment Load(Func<string, string?> lookup)
    {
        var hosts = JiraHostAllowList.Parse(lookup(JiraHostAllowList.EnvironmentVariable));
        var connection = new JiraConnection(new Uri($"https://{hosts.First()}"), lookup(EmailVariable)!);

        return new LiveEnvironment(connection, lookup(TokenVariable)!);
    }

    public static LiveEnvironment Load() => Load(Environment.GetEnvironmentVariable);

    /// <summary>A transport for the dev site; its options carry the host allowlist.</summary>
    public JiraTransport CreateTransport() =>
        new(
            Connection,
            new JiraApiToken(Token),
            null,
            JiraTransportOptions.ForLiveTests(Environment.GetEnvironmentVariable));
}
