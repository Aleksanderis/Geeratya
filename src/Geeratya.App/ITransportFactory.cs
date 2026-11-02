using Geeratya.Jira;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>Builds the transport of a connection; tests supply one that talks to a fake Jira.</summary>
public interface ITransportFactory
{
    JiraTransport Create(JiraConnection connection, JiraApiToken token);
}
