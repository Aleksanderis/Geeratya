using Geeratya.Core.State;

namespace Geeratya.App;

/// <summary>
/// Runs network work off the UI thread. Each run is stamped with the site generation when it starts and belongs to
/// a scope (a tab id); a result is posted only when the generation is still current and the run was not cancelled,
/// so a reply from a previous profile changes nothing. Cancellation is best effort; the reducer's request-id rule is
/// the other half of the guarantee.
/// </summary>
public sealed class NetworkRunner : IDisposable
{
    /// <summary>How long a network load lets the cache read of the same view post first.</summary>
    public static readonly TimeSpan CacheReadWait = TimeSpan.FromMilliseconds(250);

    private readonly SiteConnections _connections;
    private readonly ICommandSink _sink;
    private readonly CancellationToken _shutdown;
    private readonly object _lock = new();
    private readonly Dictionary<long, CancellationTokenSource> _scopes = [];
    private readonly HashSet<CancellationTokenSource> _detached = [];
    private CancellationTokenSource _all;

    /// <param name="connections">The site whose generation stamps every run.</param>
    /// <param name="sink">Thread-safe hand-over of a command to the UI thread (reducer).</param>
    /// <param name="shutdown">Cancels every run when the application ends.</param>
    public NetworkRunner(SiteConnections connections, ICommandSink sink, CancellationToken shutdown)
    {
        _connections = connections;
        _sink = sink;
        _shutdown = shutdown;
        _all = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
    }

    /// <summary>
    /// Starts <paramref name="work"/> on a background task and returns at once. Does nothing while the site has no
    /// transport, unless <paramref name="whenUnavailable"/> is given: its command is then posted (off the calling
    /// thread), so a request never waits for a reply that cannot come. The work's command, if any, is posted when
    /// the run is still current. The work maps its own Jira failures to a command. An unexpected exception of a
    /// current run (anything but its own cancellation) becomes the command of <paramref name="whenFaulted"/>, so the
    /// waiting tab ends as a failure instead of loading for ever (#924); without it the exception faults the
    /// returned task.
    /// </summary>
    public Task Start(
        long scope,
        Func<JiraServices, CancellationToken, Task<Command?>> work,
        Func<Command?>? whenUnavailable = null,
        Func<Exception, Command?>? whenFaulted = null)
    {
        return StartReporting(scope, (services, _, token) => work(services, token), whenUnavailable, whenFaulted);
    }

    /// <summary>
    /// Like <see cref="Start"/>, for work that has an intermediate result to hand over before its last command (the
    /// board configuration ahead of the first issue page). The <c>report</c> action posts a command under the same
    /// currency check as the final one: nothing is posted once the run was cancelled or the site changed.
    /// </summary>
    public Task StartReporting(
        long scope,
        Func<JiraServices, Action<Command>, CancellationToken, Task<Command?>> work,
        Func<Command?>? whenUnavailable = null,
        Func<Exception, Command?>? whenFaulted = null)
    {
        var services = JiraServices.Create(_connections);

        if (services is null)
        {
            return Unavailable(whenUnavailable);
        }

        var source = ScopeSource(scope);

        var token = source.Token;

        return Task.Run(() => RunAsync(services, work, whenFaulted, token), CancellationToken.None);
    }

    /// <summary>
    /// Starts work that needs no connection (reading the local cache) under the scope of a tab: it is cancelled with
    /// the tab or by a profile drop, and its command is posted unless that happened. Works with no transport.
    /// </summary>
    public Task StartLocal(long scope, Func<CancellationToken, Command?> work)
    {
        var token = ScopeSource(scope).Token;

        return Task.Run(() => RunLocal(work, token), CancellationToken.None);
    }

    /// <summary>
    /// Waits for a cache read started with <see cref="StartLocal"/> for at most <see cref="CacheReadWait"/>, so the
    /// cached rows normally precede the network reply, but a read stuck on slow disk I/O (a held cache lock) never
    /// delays the request without limit (#924). The read keeps running and posts when it ends.
    /// </summary>
    public static async Task AwaitCacheReadAsync(Task read)
    {
        if (read.IsCompleted)
        {
            await read;

            return;
        }

        await Task.WhenAny(read, Task.Delay(CacheReadWait, CancellationToken.None));
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

    /// <summary>
    /// Cancels the in-flight reads of every tab (quit). Detached work, that is writes that were sent, is left alone:
    /// quitting asks about writes first and then waits for them.
    /// </summary>
    public void CancelReads()
    {
        CancellationTokenSource[] sources;

        lock (_lock)
        {
            sources = [.. _scopes.Values];
            _scopes.Clear();
        }

        foreach (var source in sources)
        {
            Cancel(source);
        }
    }

    /// <summary>
    /// Like <see cref="Start"/>, for work that belongs to no tab and that closing a tab must never cancel (a write
    /// that was sent). Only <see cref="CancelAll"/> (a profile drop) and the shutdown cancel it. Nothing is kept
    /// after it ends, so a long session of writes does not pile up scopes.
    /// </summary>
    public Task StartDetached(
        Func<JiraServices, CancellationToken, Task<Command?>> work,
        Func<Command?>? whenUnavailable = null)
    {
        var services = JiraServices.Create(_connections);

        if (services is null)
        {
            return Unavailable(whenUnavailable);
        }

        CancellationTokenSource source;

        lock (_lock)
        {
            source = CancellationTokenSource.CreateLinkedTokenSource(_all.Token);
            _detached.Add(source);
        }

        return Task.Run(() => RunDetachedAsync(services, work, source), CancellationToken.None);
    }

    /// <summary>Cancels all in-flight work (<c>DropSiteCacheEffect</c>); later runs start fresh.</summary>
    public void CancelAll()
    {
        CancellationTokenSource old;
        CancellationTokenSource[] leaves;

        lock (_lock)
        {
            old = _all;
            _all = CancellationTokenSource.CreateLinkedTokenSource(_shutdown);
            leaves = [.. _scopes.Values, .. _detached];
            _scopes.Clear();
        }

        // The leaves first: a linked source learns of its parent's cancellation only when the parent's callbacks run,
        // which is off the calling thread, but the leaves must report the cancellation as soon as this returns.
        foreach (var leaf in leaves)
        {
            Cancel(leaf);
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

    private CancellationTokenSource ScopeSource(long scope)
    {
        lock (_lock)
        {
            if (!_scopes.TryGetValue(scope, out var existing))
            {
                existing = CancellationTokenSource.CreateLinkedTokenSource(_all.Token);
                _scopes[scope] = existing;
            }

            return existing;
        }
    }

    private void RunLocal(Func<CancellationToken, Command?> work, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }

        Command? result;

        try
        {
            result = work(token);
        }
        catch (Exception)
        {
            // Local work (a cache read) is best effort: a failure is a miss, never a reason to skip the network.
            return;
        }

        if (result is not null && !token.IsCancellationRequested)
        {
            _sink.Post(result);
        }
    }

    /// <summary>
    /// Marks the source cancelled before returning, but runs its callbacks and the continuations of the work waiting
    /// on its token on the thread pool. These calls come from the UI thread (a tab close, a quit, a profile switch),
    /// and a callback or continuation that runs inline there (a socket abort, a transport disposing, a handler that
    /// waits) would freeze the keys (#883).
    /// </summary>
    private static void Cancel(CancellationTokenSource? source)
    {
        try
        {
            _ = source?.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private Task Unavailable(Func<Command?>? whenUnavailable)
    {
        if (whenUnavailable is null)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() => PostUnavailable(whenUnavailable), CancellationToken.None);
    }

    private void PostUnavailable(Func<Command?> whenUnavailable)
    {
        if (!_shutdown.IsCancellationRequested && whenUnavailable() is { } command)
        {
            _sink.Post(command);
        }
    }

    private void PostIfCurrent(JiraServices services, Command command, CancellationToken token)
    {
        if (!IsObsolete(services, token))
        {
            _sink.Post(command);
        }
    }

    private bool IsObsolete(JiraServices services, CancellationToken token)
    {
        return token.IsCancellationRequested || !_connections.IsCurrent(services.Generation);
    }

    private async Task RunDetachedAsync(
        JiraServices services,
        Func<JiraServices, CancellationToken, Task<Command?>> work,
        CancellationTokenSource source)
    {
        try
        {
            await RunAsync(services, (owner, _, token) => work(owner, token), null, source.Token);
        }
        finally
        {
            lock (_lock)
            {
                _detached.Remove(source);
            }

            source.Dispose();
        }
    }

    private async Task RunAsync(
        JiraServices services,
        Func<JiraServices, Action<Command>, CancellationToken, Task<Command?>> work,
        Func<Exception, Command?>? whenFaulted,
        CancellationToken token)
    {
        Command? result;

        try
        {
            result = await work(services, command => PostIfCurrent(services, command, token), token);
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
        catch (Exception exception) when (whenFaulted is not null)
        {
            // Includes a cancellation whose token was not cancelled (an HttpClient-internal timeout): the reply
            // that would end the wait must still be posted.
            result = whenFaulted(exception);
        }

        if (result is null || IsObsolete(services, token))
        {
            return;
        }

        _sink.Post(result);
    }
}
