using Geeratya.Jira;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>The transport the application runs with: real HTTP to the connection's site.</summary>
public sealed class DefaultTransportFactory : ITransportFactory
{
    public JiraTransport Create(JiraConnection connection, JiraApiToken token)
    {
        return new JiraTransport(connection, token);
    }
}
