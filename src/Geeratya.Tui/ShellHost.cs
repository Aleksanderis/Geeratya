using Geeratya.App;
using Geeratya.Core.Cache;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
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
using Geeratya.Tui.Rendering;
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
public sealed class ShellHost : ICommandSink, IDisposable
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

    /// <summary>A pointer event: hit test and mouse table decide; the reducer runs the action like a key.</summary>
    private void OnMouse(object? sender, Mouse mouse)
    {
        var position = mouse.ScreenPosition;

        if (_dragTracker.TrackMotion(mouse.Flags, position.X, position.Y) is { } motion)
        {
            OnDragMoved(mouse, motion);

            return;
        }

        if (MouseFlagTranslator.IsHover(mouse.Flags))
        {
            OnHover(position.X, position.Y);

            return;
        }

        var live = _dragTracker.Moving;
        var drag = _dragTracker.Track(mouse.Flags, position.X, position.Y);

        if (mouse.Flags.HasFlag(MouseFlags.LeftButtonReleased))
        {
            StopDragScroll();
        }

        if (drag is null && (_dragTracker.SwallowsClick(mouse.Flags)
            || !MouseFlagTranslator.TryTranslate(mouse.Flags, out _)))
        {
            return;
        }

        var now = TimeProvider.System.GetUtcNow();
        var context = PointerContext(now);
        var commands = ResolvePointer(context, drag, live, mouse.Flags, new MouseCell(position.X, position.Y), now);

        if (commands.Count == 0)
        {
            return;
        }

        mouse.Handled = true;

        foreach (var command in commands)
        {
            Dispatch(command);
        }
    }

    // A pointer move: only the tab row cares, so the context is built only over it or to clear a hover.
    private void OnHover(int column, int row)
    {
        if (row != 0 && _state.HoveredTab is null)
        {
            return;
        }

        var hovered = MouseResolver.Hover(PointerContext(TimeProvider.System.GetUtcNow()), column, row);

        if (hovered.Tab != _state.HoveredTab)
        {
            Dispatch(hovered);
        }
    }

    private MouseContext PointerContext(DateTimeOffset now)
    {
        var size = _application!.Screen.Size;

        var regions = _screen!.OverlayRegions;

        return MouseContext.From(_state, size.Width, size.Height, FrameBuilder.HeaderRows, now, regions, _keymap) with
        {
            LaneChip = StatusBar.LaneCells(_state, size.Width, _keymap, now),
        };
    }

    // The held button moved: the range follows the pointer; outside the body a timer keeps moving the cursor.
    private void OnDragMoved(Mouse mouse, MouseDragMotion motion)
    {
        var context = PointerContext(TimeProvider.System.GetUtcNow());
        var commands = MouseResolver.ResolveDragMove(
            context,
            motion.FromColumn,
            motion.FromRow,
            motion.Column,
            motion.Row,
            motion.First);

        if (commands.Count > 0)
        {
            mouse.Handled = true;
        }

        foreach (var command in commands)
        {
            Dispatch(command);
        }

        // The speed follows the pointer on every move, also when it is outside the body and nothing else resolves.
        _dragScroller.Start(MouseResolver.DragScrollStep(context, motion.FromColumn, motion.FromRow, motion.Row));

    }

    /// <summary>Timer thread: hands one auto-scroll step to the UI thread.</summary>
    private void OnDragScrollStep(object? sender, int rows)
    {
        if (!_shutdownToken.IsCancellationRequested)
        {
            _application!.Invoke(() => StepDragScroll(rows));
        }
    }

    // On the UI thread: a step queued before the release must not move the cursor after it.
    private void StepDragScroll(int rows)
    {
        if (_dragTracker.Held)
        {
            Dispatch(new RowsDragScrolled(rows));
        }
    }

    private void StopDragScroll()
    {
        _dragScroller.Stop();
    }

    private static IReadOnlyList<AppCommand> ResolvePointer(
        MouseContext context,
        MouseDrag? drag,
        bool live,
        MouseFlags flags,
        MouseCell position,
        DateTimeOffset now)
    {
        if (drag is { } dropped)
        {
            return MouseResolver.ResolveDrag(
                context,
                dropped.FromColumn,
                dropped.FromRow,
                dropped.ToColumn,
                dropped.ToRow,
                now,
                live);
        }

        return MouseFlagTranslator.TryTranslate(flags, out var gesture)
            ? MouseResolver.Resolve(context, position.Column, position.Row, gesture, now)
            : [];
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

    /// <summary>
    /// Asks the terminal for its background before the UI owns the input, whatever theme is saved. The answer is kept
    /// for a later <c>:theme auto</c>, because the terminal cannot be asked once the UI runs.
    /// </summary>
    private void DetectBackground()
    {
        _lightBackground = _background?.IsLight(allowQuery: true);
    }

    private bool? EnvironmentBackground()
    {
        return _background?.IsLight(allowQuery: false);
    }

    /// <summary>The theme saved in the settings; a notice joins the status line when it cannot be used.</summary>
    private Theme StartTheme()
    {
        var start = _themes.Start(_state.Settings.Theme, _lightBackground);

        _state = StatusMessages.AddStartupProblems(
            _state with
            {
                ThemeNames = _themes.Names,
                SettingsInfo = new SettingsInfo(
                    _profiles.Settings?.FilePath ?? string.Empty,
                    _profiles.Keymap?.FilePath ?? string.Empty,
                    TierOf(_colors)),
            },
            start.Notice is null ? [] : [start.Notice]);

        return start.Theme;
    }

    private static ColourTier TierOf(ColorSupport colors)
    {
        return colors switch
        {
            ColorSupport.None => ColourTier.None,
            ColorSupport.Basic16 => ColourTier.Basic16,
            _ => ColourTier.TrueColour,
        };
    }

    /// <summary>Switches the screen to the chosen theme and reports the outcome for the reducer.</summary>
    private ThemeApplied ApplyTheme(string name, bool preview)
    {
        var choice = _themes.Choose(name, _state.Settings.Theme, _lightBackground ?? EnvironmentBackground());

        if (choice.Theme is not null)
        {
            _screen?.SetTheme(choice.Theme);
        }

        return new ThemeApplied(choice.Name, choice.Theme is not null, choice.Message, preview);
    }

    /// <summary>Applies the user's keymap file to the shared table and reports the outcome for the status line.</summary>
    private KeymapReloaded ReloadKeymap()
    {
        var load = UserKeymaps.Apply(_keymap, _defaultBindings, _profiles.Keymap);

        return new KeymapReloaded(load?.Notice ?? "Keymap reloaded", load?.Problems);
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

    private void OnTabsSaveFailed(string notice)
    {
        Post(new TabsSaveFailed(notice));
    }

    private void OnCacheProblem(string notice)
    {
        Post(new CacheProblem(notice));
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

        // After the connection exists, off the first-frame path: the first issue detail finds the field ids ready.
        _connections.WarmFields();
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
            ?? _savedViewEffects?.Run(effect)
            ?? _startupEffects?.Run(effect)
            ?? await _browserEffects.RunAsync(effect, _shutdownToken);
    }

    private SettingsLoad LoadSettings()
    {
        try
        {
            return _profiles.Settings?.Read() ?? new SettingsLoad(AppSettings.Default, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SettingsLoad(AppSettings.Default, []);
        }
    }


    private AppState LoadProfiles()
    {
        var settingsLoad = LoadSettings();
        var settings = settingsLoad.Settings;

        try
        {
            var loaded = _profiles.Store.Load();
            var notice = loaded.Status == ProfileLoadStatus.Corrupt ? CorruptNotice : null;

            var started = StatusMessages.Startup(
                AppState.Initial with
                {
                    Settings = settings,
                    Profiles = loaded.Catalog,
                    Session = SiteSession.Empty(loaded.Catalog.Active),
                },
                notice,
                settingsLoad.Warnings);

            // With no profile the profile manager is the first screen.
            return ProfileReducer.FirstRun(started);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return StatusMessages.Startup(
                AppState.Initial with { Settings = settings },
                $"Could not read the profiles file: {exception.Message} (adding a profile would replace it)",
                settingsLoad.Warnings);
        }
    }
}
