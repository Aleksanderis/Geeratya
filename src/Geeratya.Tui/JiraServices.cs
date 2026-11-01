using Geeratya.Jira.Transport;

namespace Geeratya.Tui;

/// <summary>
/// The Jira clients of the active site, built from its transport. Clients (search, board, filter, issue) are added
/// here as the Jira layer gains them; no DI container. An instance belongs to one connection and is never reused
/// after a profile switch.
/// </summary>
public sealed class JiraServices
{
    private JiraServices(JiraTransport transport, int generation)
    {
        Transport = transport;
        Generation = generation;
    }

    public JiraTransport Transport { get; }

    /// <summary>The <see cref="SiteConnections.Generation"/> these services were built under.</summary>
    public int Generation { get; }

    /// <summary>Services for the connected profile, or null while no transport exists (not connected yet or failed).</summary>
    public static JiraServices? Create(SiteConnections connections)
    {
        var generation = connections.Generation;
        var transport = connections.Current;

        // Generation is read first: if a switch lands in between, the stamp is older than the transport and the
        // result is dropped as stale, never the other way round.
        return transport is null ? null : new JiraServices(transport, generation);
    }
}
