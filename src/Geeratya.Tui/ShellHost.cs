using Geeratya.App;
using Geeratya.Core.Cache;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Platform.Browser;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Input;
using Geeratya.Tui.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;

using AppCommand = Geeratya.Core.State.Command;

namespace Geeratya.Tui;

/// <summary>
/// Runs the Terminal.Gui loop and connects it to the state layer: keys become commands, state becomes a frame.
/// Nothing here waits on the network; the first frame is drawn before any Jira call exists.
/// </summary>
public sealed class ShellHost : ICommandSink, IDisposable
{
    private static readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(1);

    private const string CorruptNotice =
        "profiles.json could not be read and was ignored; a copy was kept as profiles.json.corrupt.";

    private static readonly TimeSpan _quitWait = TimeSpan.FromSeconds(5);

    private readonly KeymapTable _keymap;
    private readonly string? _keymapNotice;
    private readonly KeymapTable _defaultBindings;
    private readonly ColorSupport _colors;
    private readonly GlyphSet _glyphs;
    private readonly AppReducer _reducer;
    private readonly ProfileServices _profiles;
    private readonly ProfileEffectHandler _profileEffects;
    private readonly HistoryEffectHandler _historyEffects;
    private readonly BrowserEffectHandler _browserEffects;
    private readonly SettingsEffectHandler? _settingsEffects;
    private readonly SiteConnections _connections;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly CancellationToken _shutdownToken;
    private readonly NetworkRunner _network;
    private readonly ViewLoadHandler _viewLoads;
    private readonly IssueDetailLoadHandler _detailLoads;
    private readonly SourceLoadHandler _sourceLoads;
    private readonly WriteHandler _writes;
    private readonly FormMetadataHandler _formMetadata;
    private readonly SuggestionHandler _suggestions;
    private readonly MentionCheckHandler _mentionCheck;
    private readonly ConnectionCheckHandler _connectionChecks;
    private IApplication? _application;
    private ShellScreen? _screen;
    private AppState _state = AppState.Initial;
    private ITimer? _tickTimer;
    private bool _quitting;

    public ShellHost(
        KeymapTable keymap,
        ColorSupport colors,
        ProfileServices profiles,
        GlyphSet glyphs = GlyphSet.Unicode)
    {
        _defaultBindings = new KeymapTable(keymap.Bindings);
        _keymap = keymap;
        _keymapNotice = UserKeymaps.Apply(keymap, _defaultBindings, profiles.Keymap);
        _colors = colors;
        _glyphs = glyphs;
        _profiles = profiles;
        _profileEffects = new ProfileEffectHandler(profiles);
        _settingsEffects = profiles.Settings is null ? null : new SettingsEffectHandler(profiles.Settings);
        _historyEffects = new HistoryEffectHandler(profiles.Histories);
        _browserEffects = new BrowserEffectHandler(
            BrowserLaunchers.Create(ApplicationDirectories.DetectOperatingSystem(), new ProcessRunner()));
        _connections = new SiteConnections(profiles.Tokens);
        _reducer = new AppReducer(keymap);
        _shutdownToken = _shutdown.Token;
        _network = new NetworkRunner(_connections, this, _shutdownToken);
        _viewLoads = new ViewLoadHandler(_network);
        _detailLoads = new IssueDetailLoadHandler(_network);
        _sourceLoads = new SourceLoadHandler(_network);
        _writes = new WriteHandler(_network);
        _formMetadata = new FormMetadataHandler(_network);
        _suggestions = new SuggestionHandler(_network, TimeProvider.System);
        _mentionCheck = new MentionCheckHandler(_network);
        _connectionChecks = new ConnectionCheckHandler(_connections, _network);
        profiles.Caches.WriteFailed += OnCacheWriteFailed;
        _connections.RateLimitChanged += OnRateLimitChanged;
    }

    public void Dispose()
    {
        _profiles.Caches.WriteFailed -= OnCacheWriteFailed;
        _connections.RateLimitChanged -= OnRateLimitChanged;
        _tickTimer?.Dispose();
        _profiles.Caches.Dispose();
        _network.Dispose();
        _connections.Dispose();
        _shutdown.Dispose();
    }

    public int Run()
    {
        using var application = Application.Create();

        _state = LoadProfiles();
        _state = _state with { Message = JoinNotices(_state.Message, _keymapNotice) };
        _application = application;
        application.Init();
        application.Keyboard.KeyBindings.Clear();

        if (_colors == ColorSupport.Basic16)
        {
            application.Driver!.Force16Colors = true;
        }

        application.Keyboard.KeyDown += OnKeyDown;
        application.Paste += OnPaste;

        using var screen = new ShellScreen(_keymap, _colors, application.Screen.Size, _glyphs);

        _screen = screen;
        screen.BodyRowsChanged += OnBodyRowsChanged;
        screen.Render(_state, TimeProvider.System.GetUtcNow());
        Dispatch(new ViewportChanged(screen.BodyRows, screen.BodyColumns));
        Dispatch(new ConnectionCheckRequested());
        _tickTimer = TimeProvider.System.CreateTimer(PostTick, null, _tickInterval, _tickInterval);
        application.Run(screen);
        _shutdown.Cancel();
        _profiles.Caches.FlushAll();

        return 0;
    }

    /// <summary>Stops the application, after waiting up to five seconds for running writes to settle.</summary>
    private async Task QuitAsync()
    {
        if (_quitting)
        {
            return;
        }

        _quitting = true;

        if (_writes.HasRunning)
        {
            _state = _state with { Message = "Waiting for running writes to finish (up to 5 s) ..." };
            _screen!.Render(_state, TimeProvider.System.GetUtcNow());

            await _writes.WaitForRunningAsync(_quitWait, CancellationToken.None);
        }

        _application!.Invoke(_application.RequestStop);
    }

    /// <summary>Thread-safe entry for background work: runs the reducer on the UI thread.</summary>
    public void Post(AppCommand command)
    {
        if (_shutdownToken.IsCancellationRequested)
        {
            return;
        }

        _application!.Invoke(() => Dispatch(command));
    }

    private void OnBodyRowsChanged(int rows)
    {
        Dispatch(new ViewportChanged(rows, _screen!.BodyColumns));
    }

    /// <summary>Timer callback (not the UI thread): hands the clock to the reducer, which decides about refreshes.</summary>
    private void PostTick(object? timerState)
    {
        Post(new Tick(TimeProvider.System.GetUtcNow()));
    }

    /// <summary>The transport's rate-limit wait started or ended (not the UI thread).</summary>
    private void OnRateLimitChanged(DateTimeOffset? until)
    {
        Post(new LinkChanged(SiteLink.FromRateLimit(until)));
    }

    private void OnKeyDown(object? sender, Key key)
    {
        var spelled = KeyTranslator.Translate(key);

        if (spelled is null)
        {
            return;
        }

        key.Handled = true;

        Dispatch(new KeyPressed(spelled, TimeProvider.System.GetUtcNow()));
    }

    /// <summary>Bracketed paste arrives as one event, not as keys; the focused view must not also receive it.</summary>
    private void OnPaste(object? sender, PasteEventArgs e)
    {
        e.Handled = true;

        Dispatch(new TextPasted(e.Text));
    }

    private void Dispatch(AppCommand command)
    {
        var result = _reducer.Reduce(_state, command);

        // An idle tick changes nothing: no redraw, unless the banner counts time (a wait, the age of the data).
        if (command is Tick tick && ReferenceEquals(result.State, _state) && result.Effects.Count == 0
            && !TabStatus.Of(_state.Session, tick.Now).ShowsTime)
        {
            return;
        }

        _state = result.State;
        _screen!.Render(_state, TimeProvider.System.GetUtcNow());

        var checks = new List<CheckConnectionEffect>();
        KeymapReloaded? reloaded = null;

        foreach (var effect in result.Effects)
        {
            if (effect is QuitEffect)
            {
                _ = QuitAsync();
            }
            else if (effect is DropSiteCacheEffect drop)
            {
                _network.CancelAll();
                _profiles.Caches.Drop(drop.Profile);
                _connections.Drop(drop.Profile);
            }
            else if (effect is CancelTabEffect cancel)
            {
                _network.CancelScope(cancel.Tab.Value);
            }
            else if (effect is LoadViewPageEffect load)
            {
                _ = _viewLoads.StartAsync(load, ActiveCache());
            }
            else if (effect is LoadIssueDetailEffect or LoadIssueCommentsEffect or LoadRemoteLinksEffect
                or LoadIssueChildrenEffect)
            {
                _ = _detailLoads.StartAsync(effect, ActiveCache());
            }
            else if (effect is LoadSourcesEffect sources)
            {
                _ = _sourceLoads.StartAsync(sources);
            }
            else if (effect is ExecuteWriteEffect or VerifyWriteEffect or CountSubtasksEffect)
            {
                _ = _writes.StartAsync(effect);
            }
            else if (effect is LoadFormMetadataEffect formMetadata)
            {
                _ = _formMetadata.StartAsync(formMetadata);
            }
            else if (effect is SearchSuggestionsEffect search)
            {
                _ = _suggestions.StartAsync(search);
            }
            else if (effect is CheckMentionsEffect mentions)
            {
                _ = _mentionCheck.StartAsync(mentions);
            }
            else if (effect is CancelSuggestionsEffect stop)
            {
                _suggestions.Cancel(stop.Tab);
            }
            else if (effect is InvalidateCacheEffect invalidate)
            {
                InvalidateCaches(invalidate);
            }
            else if (effect is WriteCacheEffect write)
            {
                ActiveCache()?.Put(write.Key, write.View);
            }
            else if (effect is RemoveIssueCacheEffect removeIssue)
            {
                ActiveCache()?.RemoveIssue(removeIssue.Key);
            }
            else if (effect is WriteIssueCacheEffect writeIssue)
            {
                ActiveCache()?.PutIssue(writeIssue.Issue);
            }
            else if (effect is ReloadKeymapEffect)
            {
                reloaded = ReloadKeymap();
            }
            else if (effect is CheckConnectionEffect check)
            {
                checks.Add(check);
            }
            else
            {
                _ = RunEffectAsync(effect);
            }
        }

        if (checks.Count > 0 || command is ProfilesChanged)
        {
            // A check or a profile change (a token stored again) retries a failed profile; the failure reply must not.
            _connections.RetryFailed();
        }

        _ = ConnectAsync();

        if (reloaded is not null)
        {
            Dispatch(reloaded);
        }

        // After ConnectAsync, so the check waits for the connection the same dispatch asked for.
        foreach (var check in checks)
        {
            _ = _connectionChecks.StartAsync(check);
        }
    }

    /// <summary>Applies the user's keymap file to the shared table and reports the outcome for the status line.</summary>
    private KeymapReloaded ReloadKeymap()
    {
        var notice = UserKeymaps.Apply(_keymap, _defaultBindings, _profiles.Keymap);

        _screen?.RefreshHelp();

        return new KeymapReloaded(notice ?? "Keymap reloaded");
    }

    /// <summary>Drops the cached details of written issues and every cached first page of the profile.</summary>
    private void InvalidateCaches(InvalidateCacheEffect effect)
    {
        var cache = ActiveCache();

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

    /// <summary>The cache of the active profile, or null while there is none.</summary>
    private SiteCache? ActiveCache()
    {
        var profile = _state.Profiles.Active;

        return profile is null ? null : _profiles.Caches.For(profile);
    }

    private void OnCacheWriteFailed(string notice)
    {
        Post(new CacheWriteFailed(notice));
    }

    /// <summary>Builds the connection for the active profile off the UI thread; no-op when already built.</summary>
    private async Task ConnectAsync()
    {
        var failure = await _connections.ActivateAsync(
            _state.Profiles.Active,
            _state.Profiles.Profiles.Count,
            _shutdownToken);

        if (failure is not null && !_shutdownToken.IsCancellationRequested)
        {
            _application!.Invoke(() => Dispatch(failure));
        }
    }

    /// <summary>Runs a profile effect off the UI thread and feeds its outcome back as a command.</summary>
    private async Task RunEffectAsync(Effect effect)
    {
        var outcome = await Task.Run(() => RunHandlersAsync(effect));

        if (outcome is not null && !_shutdownToken.IsCancellationRequested)
        {
            _application!.Invoke(() => Dispatch(outcome));
        }
    }

    private async Task<AppCommand?> RunHandlersAsync(Effect effect)
    {
        return _settingsEffects?.Run(effect)
            ?? await _profileEffects.RunAsync(effect, _shutdownToken)
            ?? _historyEffects.Run(effect)
            ?? await _browserEffects.RunAsync(effect, _shutdownToken);
    }

    private AppSettings LoadSettings()
    {
        try
        {
            return _profiles.Settings?.Load() ?? AppSettings.Default;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }
    }

    private static string? JoinNotices(string? first, string? second)
    {
        return first is not null && second is not null ? $"{first} | {second}" : first ?? second;
    }

    private AppState LoadProfiles()
    {
        var settings = LoadSettings();

        try
        {
            var loaded = _profiles.Store.Load();
            var notice = loaded.Status == ProfileLoadStatus.Corrupt ? CorruptNotice : null;

            return AppState.Initial with
            {
                Settings = settings,
                Profiles = loaded.Catalog,
                Session = SiteSession.Empty(loaded.Catalog.Active),
                Message = notice,
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AppState.Initial with
            {
                Settings = settings,
                Message = $"Could not read the profiles file: {exception.Message} (adding a profile would replace it)",
            };
        }
    }
}
