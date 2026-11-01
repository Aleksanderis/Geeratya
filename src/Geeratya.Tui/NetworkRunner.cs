using Geeratya.Core.State;

namespace Geeratya.Tui;

/// <summary>
/// Runs network work off the UI thread. Each run is stamped with the site generation when it starts and belongs to
/// a scope (a tab id); a result is posted only when the generation is still current and the run was not cancelled,
/// so a reply from a previous profile changes nothing. Cancellation is best effort; the reducer's request-id rule is
/// the other half of the guarantee.
/// </summary>
public sealed class NetworkRunner : IDisposable
{
    private readonly SiteConnections _connections;
    private readonly Action<Command> _post;
    private readonly CancellationToken _shutdown;
    private readonly object _lock = new();
    private readonly Dictionary<long, CancellationTokenSource> _scopes = [];
    private CancellationTokenSource _all;

    /// <param name="connections">The site whose generation stamps every run.</param>
    /// <param name="post">Thread-safe hand-over of a command to the UI thread (reducer).</param>
    /// <param name="shutdown">Cancels every run when the application ends.</param>
    public NetworkRunner(SiteConnections connections, Action<Command> post, CancellationToken shutdown)
    {
        _connections = connections;
        _post = post;
        _shutdown = shutdown;
        _all = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
    }

    /// <summary>
    /// Starts <paramref name="work"/> on a background task and returns at once. Does nothing while the site has no
    /// transport. The work's command, if any, is posted when the run is still current. The work maps its own Jira
    /// failures to a command; an unexpected exception of a current run faults the returned task.
    /// </summary>
    public Task Start(long scope, Func<JiraServices, CancellationToken, Task<Command?>> work)
    {
        var services = JiraServices.Create(_connections);

        if (services is null)
        {
            return Task.CompletedTask;
        }

        CancellationTokenSource source;

        lock (_lock)
        {
            if (!_scopes.TryGetValue(scope, out var existing))
            {
                existing = CancellationTokenSource.CreateLinkedTokenSource(_all.Token);
                _scopes[scope] = existing;
            }

            source = existing;
        }

        var token = source.Token;

        return Task.Run(() => RunAsync(services, work, token), CancellationToken.None);
    }

    /// <summary>Cancels the in-flight work of one tab (<c>CancelTabEffect</c>).</summary>
    public void CancelScope(long scope)
    {
        CancellationTokenSource? source;

        lock (_lock)
        {
            _scopes.Remove(scope, out source);
        }

        Cancel(source);
    }

    /// <summary>Cancels all in-flight work (<c>DropSiteCacheEffect</c>); later runs start fresh.</summary>
    public void CancelAll()
    {
        CancellationTokenSource old;

        lock (_lock)
        {
            old = _all;
            _all = CancellationTokenSource.CreateLinkedTokenSource(_shutdown);
            _scopes.Clear();
        }

        Cancel(old);
    }

    public void Dispose()
    {
        CancelAll();

        lock (_lock)
        {
            _all.Dispose();
        }
    }

    private static void Cancel(CancellationTokenSource? source)
    {
        try
        {
            source?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private bool IsObsolete(JiraServices services, CancellationToken token)
    {
        return token.IsCancellationRequested || !_connections.IsCurrent(services.Generation);
    }

    private async Task RunAsync(
        JiraServices services,
        Func<JiraServices, CancellationToken, Task<Command?>> work,
        CancellationToken token)
    {
        Command? result;

        try
        {
            result = await work(services, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception) when (IsObsolete(services, token))
        {
            // The old transport is disposed on a switch, so work still running may fail with anything.
            return;
        }

        if (result is null || IsObsolete(services, token))
        {
            return;
        }

        _post(result);
    }
}
