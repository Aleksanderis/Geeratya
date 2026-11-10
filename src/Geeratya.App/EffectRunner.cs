using Geeratya.App.Updates;
using Geeratya.Core.Cache;
using Geeratya.Core.Clipboard;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Platform.Browser;
using Geeratya.Platform.Clipboard;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;

namespace Geeratya.App;

/// <summary>
/// Routes every effect the reducer asks for, except the terminal-bound ones (quit, theme, keymap reload and the
/// connection check, which the shell orders around the connection), to the handler that performs it. Owns the
/// handlers; replies reach the reducer through the <see cref="ICommandSink"/>. Network handlers share the
/// <see cref="NetworkRunner"/>, which holds the cancellation and generation rules.
/// </summary>
public sealed class EffectRunner : IDisposable
{
    private readonly ProfileServices _profiles;
    private readonly SiteConnections _connections;
    private readonly NetworkRunner _network;
    private readonly ICommandSink _sink;
    private readonly CancellationToken _shutdown;
    private readonly Dictionary<Type, Action<Effect, Profile?>> _routes = [];
    private readonly List<ILocalEffectHandler> _local = [];
    private readonly StartupEffectHandler? _startup;
    private readonly ViewLoadHandler _viewLoads;
    private readonly IssueDetailLoadHandler _detailLoads;
    private readonly SourceLoadHandler _sourceLoads;
    private readonly WriteHandler _writes;
    private readonly FormMetadataHandler _formMetadata;
    private readonly SuggestionHandler _suggestions;
    private readonly MentionCheckHandler _mentionCheck;
    private readonly ConnectionCheckHandler _connectionChecks;

    /// <param name="profiles">The stores and caches the local handlers and the cache effects use.</param>
    /// <param name="connections">The site connection that a dropped profile releases.</param>
    /// <param name="network">Runs the network handlers; shared with the host, which cancels reads at quit.</param>
    /// <param name="sink">Thread-safe hand-over of a reply to the reducer.</param>
    /// <param name="shutdown">Cancels local work when the application ends.</param>
    /// <param name="browser">The launcher for <see cref="OpenUrlEffect"/>; the platform's own by default.</param>
    /// <param name="clipboard">
    /// The clipboard for <see cref="CopyToClipboardEffect"/>; the platform's own by default.
    /// </param>
    public EffectRunner(
        ProfileServices profiles,
        SiteConnections connections,
        NetworkRunner network,
        ICommandSink sink,
        CancellationToken shutdown,
        IBrowserLauncher? browser = null,
        IClipboard? clipboard = null)
    {
        _profiles = profiles;
        _connections = connections;
        _network = network;
        _sink = sink;
        _shutdown = shutdown;
        _viewLoads = new ViewLoadHandler(network);
        _detailLoads = new IssueDetailLoadHandler(network);
        _sourceLoads = new SourceLoadHandler(network);
        _writes = new WriteHandler(network);
        _formMetadata = new FormMetadataHandler(network);
        _suggestions = new SuggestionHandler(network, TimeProvider.System);
        _mentionCheck = new MentionCheckHandler(network);
        _connectionChecks = new ConnectionCheckHandler(connections, network);
        _startup = profiles.Tabs is { } tabs ? new StartupEffectHandler(tabs) : null;

        _startup?.SaveFailed += OnTabsSaveFailed;

        AddLocalHandlers(browser, clipboard);
        AddNetworkRoutes();
        AddCacheRoutes();
    }

    /// <summary>Whether a write is still running, so a quit has to wait for it.</summary>
    public bool HasRunningWrites => _writes.HasRunning;

    /// <summary>Waits until the running writes finish; false when the timeout passed first.</summary>
    public Task<bool> WaitForWritesAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return _writes.WaitForRunningAsync(timeout, cancellationToken);
    }

    /// <summary>Writes the queued tab list to disk (at quit).</summary>
    public void FlushTabs()
    {
        _startup?.Flush();
    }

    /// <summary>Whether <see cref="Run"/> knows what to do with <paramref name="effect"/>.</summary>
    public bool IsRouted(Effect effect)
    {
        return _routes.ContainsKey(effect.GetType()) || FindLocal(effect) is not null;
    }

    /// <summary>Starts the work of <paramref name="effect"/> and returns at once.</summary>
    /// <param name="effect">The effect to perform.</param>
    /// <param name="active">The active profile, whose cache the cache effects use; null while there is none.</param>
    public void Run(Effect effect, Profile? active)
    {
        if (_routes.TryGetValue(effect.GetType(), out var route))
        {
            route(effect, active);

            return;
        }

        if (FindLocal(effect) is { } handler)
        {
            _ = RunLocalAsync(handler, effect);
        }
    }

    /// <summary>Adds a local handler after the built-in ones (an extra seam for a host or a test).</summary>
    public void AddLocalHandler(ILocalEffectHandler handler)
    {
        _local.Add(handler);
    }

    /// <summary>Starts the connection check; the host calls it once the connection of the same step exists.</summary>
    public Task StartConnectionCheckAsync(CheckConnectionEffect effect)
    {
        return _connectionChecks.StartAsync(effect);
    }

    public void Dispose()
    {
        _startup?.SaveFailed -= OnTabsSaveFailed;
    }

    private void AddLocalHandlers(IBrowserLauncher? browser, IClipboard? clipboard)
    {
        // The order is the order of the old handler chain; the handlers claim disjoint effects.
        if (_profiles.Settings is { } settings)
        {
            _local.Add(new SettingsEffectHandler(settings));
        }

        _local.Add(new ProfileEffectHandler(_profiles));
        _local.Add(new HistoryEffectHandler(_profiles.Histories));

        if (_profiles.SavedViews is { } savedViews)
        {
            _local.Add(new SavedViewEffectHandler(savedViews));
        }

        if (_startup is not null)
        {
            _local.Add(_startup);
        }

        var launcher = browser
            ?? BrowserLaunchers.Create(ApplicationDirectories.DetectOperatingSystem(), new ProcessRunner());

        if (_profiles.StateDirectory is { } stateDirectory)
        {
            _local.Add(UpdateCheckHandler.CreateForProcess(stateDirectory));
        }

        _local.Add(new BrowserEffectHandler(launcher));
        _local.Add(new ClipboardEffectHandler(clipboard ?? Clipboards.ForProcess()));
    }

    private void AddNetworkRoutes()
    {
        AddAsync<LoadViewPageEffect>((effect, active) => _viewLoads.StartAsync(effect, CacheOf(active)));
        AddAsync<LoadQuickFiltersEffect>((effect, _) => _viewLoads.StartQuickFiltersAsync(effect));
        AddAsync<LoadIssueDetailEffect>((effect, active) => _detailLoads.StartAsync(effect, CacheOf(active)));
        AddAsync<LoadIssueCommentsEffect>((effect, active) => _detailLoads.StartAsync(effect, CacheOf(active)));
        AddAsync<LoadRemoteLinksEffect>((effect, active) => _detailLoads.StartAsync(effect, CacheOf(active)));
        AddAsync<LoadEditableFieldsEffect>((effect, active) => _detailLoads.StartAsync(effect, CacheOf(active)));
        AddAsync<LoadSubtaskAssigneesEffect>((effect, active) => _detailLoads.StartAsync(effect, CacheOf(active)));
        AddAsync<LoadIssueChildrenEffect>((effect, active) => _detailLoads.StartAsync(effect, CacheOf(active)));
        AddAsync<LoadSourcesEffect>((effect, _) => _sourceLoads.StartAsync(effect));
        AddAsync<LoadSprintsEffect>((effect, _) => _sourceLoads.StartSprintsAsync(effect));
        AddAsync<ExecuteWriteEffect>((effect, _) => _writes.StartAsync(effect));
        AddAsync<VerifyWriteEffect>((effect, _) => _writes.StartAsync(effect));
        AddAsync<CountSubtasksEffect>((effect, _) => _writes.StartAsync(effect));
        AddAsync<LoadFormMetadataEffect>((effect, _) => _formMetadata.StartAsync(effect));
        AddAsync<SearchSuggestionsEffect>((effect, _) => _suggestions.StartAsync(effect));
        AddAsync<CheckMentionsEffect>((effect, _) => _mentionCheck.StartAsync(effect));
        Add<CancelSuggestionsEffect>((effect, _) => _suggestions.Cancel(effect.Tab));
        Add<CancelTabEffect>((effect, _) => _network.CancelScope(effect.Tab.Value));
        Add<DropSiteCacheEffect>((effect, _) => DropSite(effect.Profile));
    }

    private void AddCacheRoutes()
    {
        Add<InvalidateCacheEffect>(InvalidateCaches);
        Add<WriteCacheEffect>((effect, active) => CacheOf(active)?.Put(effect.Key, effect.View));
        Add<RemoveIssueCacheEffect>((effect, active) => CacheOf(active)?.RemoveIssue(effect.Key));
        Add<WriteIssueCacheEffect>((effect, active) => CacheOf(active)?.PutIssue(effect.Issue));
        Add<SaveTabsEffect>((effect, _) => _startup?.Queue(effect));
    }

    private void Add<TEffect>(Action<TEffect, Profile?> route)
        where TEffect : Effect
    {
        _routes[typeof(TEffect)] = (effect, active) => route((TEffect)effect, active);
    }

    private void AddAsync<TEffect>(Func<TEffect, Profile?, Task> start)
        where TEffect : Effect
    {
        _routes[typeof(TEffect)] = (effect, active) => _ = start((TEffect)effect, active);
    }

    private ILocalEffectHandler? FindLocal(Effect effect)
    {
        foreach (var handler in _local)
        {
            if (handler.CanRun(effect))
            {
                return handler;
            }
        }

        return null;
    }

    /// <summary>Runs a local effect off the UI thread and feeds its outcome back as a command.</summary>
    private async Task RunLocalAsync(ILocalEffectHandler handler, Effect effect)
    {
        Command? outcome;

        try
        {
            outcome = await Task.Run(() => handler.RunAsync(effect, _shutdown), CancellationToken.None);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            // A throwing local effect must still answer whoever waits for it (a tab list that never loads).
            outcome = handler.Faulted(effect, exception);
        }

        if (outcome is not null)
        {
            _sink.Post(outcome);
        }
    }

    private void DropSite(Profile profile)
    {
        _network.CancelAll();
        _profiles.Caches.Drop(profile);
        _connections.Drop(profile);
    }

    /// <summary>Drops the cached details of written issues and every cached first page of the profile.</summary>
    private void InvalidateCaches(InvalidateCacheEffect effect, Profile? active)
    {
        var cache = CacheOf(active);

        if (cache is null)
        {
            return;
        }

        foreach (var key in effect.Keys)
        {
            cache.RemoveIssue(key);
        }

        cache.RemoveViews();
    }

    private SiteCache? CacheOf(Profile? active)
    {
        return active is null ? null : _profiles.Caches.For(active);
    }

    private void OnTabsSaveFailed(string notice)
    {
        _sink.Post(new TabsSaveFailed(notice));
    }
}
