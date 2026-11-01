using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Fakes;

/// <summary>A transport over a <see cref="FakeJiraHandler"/> whose waits are recorded instead of slept.</summary>
public sealed class TransportFixture : IDisposable
{
    public const string Email = "user1@example.com";
    public const string Token = "fake-token-SECRET-123";
    public const string Host = "example.atlassian.net";

    private readonly List<TimeSpan> _waits = [];

    public TransportFixture(Func<JiraTransportOptions, JiraTransportOptions>? configure = null)
    {
        Handler = new FakeJiraHandler();
        Connection = new JiraConnection(new Uri($"https://{Host}"), Email);

        var options = new JiraTransportOptions
        {
            Delay = RecordWait,
            Jitter = () => 0.5,
            Timeout = TimeSpan.FromSeconds(5),
        };

        Transport = new JiraTransport(
            Connection,
            new JiraApiToken(Token),
            Handler,
            configure is null ? options : configure(options));
    }

    public FakeJiraHandler Handler { get; }

    public JiraConnection Connection { get; }

    public JiraTransport Transport { get; }

    public IReadOnlyList<TimeSpan> Waits => _waits;

    public void Dispose() => Transport.Dispose();

    private Task RecordWait(TimeSpan wait, CancellationToken cancellationToken)
    {
        _waits.Add(wait);

        return Task.CompletedTask;
    }
}
