using Geeratya.Jira;
using Geeratya.Jira.Transport;

namespace Geeratya.App.PrintFrame;

/// <summary>
/// A transport factory whose transports refuse every host outside the allowlist (<c>GEERATYA_LIVE_HOSTS</c>).
/// </summary>
public sealed class AllowListedTransportFactory : ITransportFactory
{
    private readonly JiraTransportOptions _options;

    public AllowListedTransportFactory(IReadOnlySet<string> allowedHosts)
    {
        _options = new JiraTransportOptions { AllowedHosts = allowedHosts };
    }

    public JiraTransport Create(JiraConnection connection, JiraApiToken token)
    {
        return new JiraTransport(connection, token, options: _options);
    }
}
