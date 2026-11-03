using Geeratya.App;
using Geeratya.Core.Cache;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Platform.Browser;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Input;
using Geeratya.Tui.Rendering.Themes;
using Geeratya.Tui.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;

using AppCommand = Geeratya.Core.State.Command;

namespace Geeratya.Tui;

/// <summary>
/// Runs the Terminal.Gui loop and connects it to the state layer: keys become commands, state becomes a frame.
/// Nothing here waits on the network; the first frame is drawn before any Jira call exists.
/// </summary>
public sealed partial class ShellHost : ICommandSink, IDisposable
{
    private static readonly TimeSpan _whichKeyMargin = TimeSpan.FromMilliseconds(5);

    private static readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(1);

    private const string CorruptNotice =
        "profiles.json could not be read and was ignored; a copy was kept as profiles.json.corrupt.";

    private static readonly TimeSpan _quitWait = TimeSpan.FromSeconds(5);

    private readonly KeymapTable _keymap;

    private readonly ThemeSwitcher _themes;

    private readonly MouseDragTracker _dragTracker = new();

    private readonly KeymapLoadResult? _keymapLoad;

    private readonly KeymapTable _defaultBindings;

    private readonly ColorSupport _colors;

    private readonly GlyphSet _glyphs;

    private readonly AppReducer _reducer;

    private readonly ProfileServices _profiles;

    private readonly ProfileEffectHandler _profileEffects;

    private readonly HistoryEffectHandler _historyEffects;

    private readonly SavedViewEffectHandler? _savedViewEffects;

    private readonly StartupEffectHandler? _startupEffects;

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

    private readonly TerminalBackgroundDetector? _background;

    private bool? _lightBackground;

    private IApplication? _application;

    private ShellScreen? _screen;

    private AppState _state = AppState.Initial;

    private ITimer? _tickTimer;

    private ITimer? _whichKeyTimer;

    private readonly DragAutoScroller _dragScroller = new(TimeProvider.System);

    private DateTimeOffset? _whichKeyTimedFor;

    private bool _chipShown;

    private bool _quitting;

    public ShellHost(
        KeymapTable keymap,
        ColorSupport colors,
        ProfileServices profiles,
        GlyphSet glyphs = GlyphSet.Unicode,
        TerminalBackgroundDetector? background = null)
    {
        _background = background;
        _defaultBindings = new KeymapTable(keymap.Bindings);
        _keymap = keymap;
        _keymapLoad = UserKeymaps.Apply(keymap, _defaultBindings, profiles.Keymap);
        _colors = colors;
        _themes = new ThemeSwitcher(new ThemeCatalog(profiles.ThemesDirectory));
        _glyphs = glyphs;
        _profiles = profiles;
        _profileEffects = new ProfileEffectHandler(profiles);
        _settingsEffects = profiles.Settings is null ? null : new SettingsEffectHandler(profiles.Settings);
        _historyEffects = new HistoryEffectHandler(profiles.Histories);
        _savedViewEffects = profiles.SavedViews is { } savedViews ? new SavedViewEffectHandler(savedViews) : null;
        _startupEffects = profiles.Tabs is { } tabs ? new StartupEffectHandler(tabs) : null;
        _browserEffects = new BrowserEffectHandler(
            BrowserLaunchers.Create(ApplicationDirectories.DetectOperatingSystem(), new ProcessRunner()));
        _connections = new SiteConnections(profiles.Tokens, boardKinds: profiles.BoardKinds);
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
        profiles.Caches.WriteFailed += OnCacheProblem;
        profiles.Caches.ReadProblem += OnCacheProblem;

        _startupEffects?.SaveFailed += OnTabsSaveFailed;

        _connections.RateLimitChanged += OnRateLimitChanged;
        _dragScroller.Stepped += OnDragScrollStep;
    }

    public void Dispose()
    {
        _profiles.Caches.WriteFailed -= OnCacheProblem;
        _profiles.Caches.ReadProblem -= OnCacheProblem;

        _startupEffects?.SaveFailed -= OnTabsSaveFailed;

        _connections.RateLimitChanged -= OnRateLimitChanged;
        _tickTimer?.Dispose();
        _whichKeyTimer?.Dispose();
        _dragScroller.Stepped -= OnDragScrollStep;
        _dragScroller.Dispose();
        _profiles.Caches.Dispose();
        _network.Dispose();
        _connections.Dispose();
        _shutdown.Dispose();
    }

    public int Run()
    {
        using var application = Application.Create();

        _state = LoadProfiles();
        _state = StatusMessages.KeymapStartup(_state, _keymapLoad);
        _application = application;
        DetectBackground();
        application.Init();
        application.Keyboard.KeyBindings.Clear();

        if (_colors == ColorSupport.Basic16)
        {
            application.Driver!.Force16Colors = true;
        }

        application.Keyboard.KeyDown += OnKeyDown;
        application.Paste += OnPaste;
        application.Mouse.MouseEvent += OnMouse;

        var startTheme = StartTheme();

        using var screen = new ShellScreen(_keymap, _colors, application.Screen.Size, startTheme, _glyphs);

        _screen = screen;
        screen.BodyRowsChanged += OnBodyRowsChanged;
        screen.Render(_state, TimeProvider.System.GetUtcNow());
        Dispatch(new ViewportChanged(screen.BodyRows, screen.BodyColumns, screen.BodyHeight));
        Dispatch(new ConnectionCheckRequested());
        Dispatch(new StartupRequested());
        _tickTimer = TimeProvider.System.CreateTimer(PostTick, null, _tickInterval, _tickInterval);
        application.Run(screen);
        _shutdown.Cancel();
        _profiles.Caches.FlushAll();
        _startupEffects?.Flush();

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
        _network.CancelReads();

        if (_writes.HasRunning)
        {
            _state = _state with { Message = "Waiting for running writes to finish (up to 5 s) ...", Status = null };
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
        Dispatch(new ViewportChanged(rows, _screen!.BodyColumns, _screen.BodyHeight));
    }

    /// <summary>Timer callback (not the UI thread): hands the clock to the reducer, which decides about refreshes.</summary>
    private void PostTick(object? timerState)
    {
        Post(new Tick(TimeProvider.System.GetUtcNow()));
    }

    /// <summary>
    /// Wakes the reducer once when the which-key delay of a new pending chord has passed (the state layer has no
    /// timer), so the popup opens on time instead of at the next one-second tick.
    /// </summary>
    private void ScheduleWhichKey()
    {
        if (_state.PendingKeys.Length == 0 || _state.PendingSince == _whichKeyTimedFor)
        {
            return;
        }

        _whichKeyTimedFor = _state.PendingSince;
        _whichKeyTimer?.Dispose();
        _whichKeyTimer = TimeProvider.System.CreateTimer(
            PostTick,
            null,
            WhichKeyDelay.Delay + _whichKeyMargin,
            System.Threading.Timeout.InfiniteTimeSpan);
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

    private void Dispatch(AppCommand command)
    {
        var result = _reducer.Reduce(_state, command);

        // An idle tick changes nothing: no redraw, unless the banner counts time (a wait, the age of the data).
        if (command is Tick tick && ReferenceEquals(result.State, _state) && result.Effects.Count == 0
            && !TabStatus.Of(_state.Session, tick.Now).ShowsTime
            && StatusMessages.IsFresh(_state, tick.Now) == _chipShown)
        {
            return;
        }

        _state = result.State;

        var renderedAt = TimeProvider.System.GetUtcNow();

        _chipShown = StatusMessages.IsFresh(_state, renderedAt);
        _screen!.Render(_state, renderedAt);
        ScheduleWhichKey();

        var checks = new List<CheckConnectionEffect>();
        KeymapReloaded? reloaded = null;
        ThemeApplied? themed = null;

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
            else if (effect is LoadQuickFiltersEffect quickFilters)
            {
                _ = _viewLoads.StartQuickFiltersAsync(quickFilters);
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
            else if (effect is LoadSprintsEffect sprints)
            {
                _ = _sourceLoads.StartSprintsAsync(sprints);
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
            else if (effect is SaveTabsEffect saveTabs)
            {
                _startupEffects?.Queue(saveTabs);
            }
            else if (effect is ReloadKeymapEffect)
            {
                reloaded = ReloadKeymap();
            }
            else if (effect is ApplyThemeEffect themeEffect)
            {
                themed = ApplyTheme(themeEffect.Name, themeEffect.Preview);
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

        if (themed is not null)
        {
            Dispatch(themed);
        }

        // After ConnectAsync, so the check waits for the connection the same dispatch asked for.
        foreach (var check in checks)
        {
            _ = _connectionChecks.StartAsync(check);
        }
    }
}
