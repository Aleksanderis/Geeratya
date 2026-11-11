using Geeratya.Jira.Gateway;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>
/// The transport and cutoff reader a profile switch took out of <see cref="SiteConnections"/>. Disposing them closes
/// sockets and cancels requests in flight, so it never happens inside the connections' lock or on the UI thread.
/// </summary>
internal sealed record RetiredConnection(JiraTransport? Transport, DoneCutoffReader? Cutoffs)
{
    /// <summary>Disposes both on the thread pool, so the caller returns at once.</summary>
    public void DisposeOnThreadPool()
    {
        if (Transport is null && Cutoffs is null)
        {
            return;
        }

        _ = Task.Run(Dispose);
    }

    /// <summary>Disposes both on the calling thread.</summary>
    public void Dispose()
    {
        try
        {
            Transport?.Dispose();
        }
        finally
        {
            Cutoffs?.Dispose();
        }
    }
}
