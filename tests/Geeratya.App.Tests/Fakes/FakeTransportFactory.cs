using Geeratya.Jira;
using Geeratya.Jira.Transport;

namespace Geeratya.App.Tests.Fakes;

/// <summary>Builds transports that send through the given handler instead of the network.</summary>
public sealed class FakeTransportFactory : ITransportFactory
{
    private readonly HttpMessageHandler _http;
    private readonly JiraTransportOptions? _options;

    public FakeTransportFactory(HttpMessageHandler http, JiraTransportOptions? options = null)
    {
        _http = http;
        _options = options;
    }

    public JiraTransport Create(JiraConnection connection, JiraApiToken token)
    {
        return new JiraTransport(connection, token, _http, _options);
    }
}
