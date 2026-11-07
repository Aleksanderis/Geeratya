using Geeratya.App;
using Geeratya.Core.Comments;
using Geeratya.Core.Keymap;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Platform;
using Geeratya.Platform.Clipboard;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Input;
using Geeratya.Tui.Rendering.Themes;
using Geeratya.Tui.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;

using AppCommand = Geeratya.Core.State.Command;
using IClipboard = Geeratya.Platform.Clipboard.IClipboard;

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
    private ColorSupport _colors;
    private readonly GlyphSet _glyphs;
    private readonly AppReducer _reducer;
    private readonly ProfileServices _profiles;
    private readonly SiteConnections _connections;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly CancellationToken _shutdownToken;
    private readonly NetworkRunner _network;
    private readonly EffectRunner _effects;
    private readonly TerminalBackgroundDetector? _background;
    private bool? _lightBackground;
    private readonly ApplicationUiThread _uiThread = new();
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
        _connections = new SiteConnections(profiles.Tokens, boardKinds: profiles.BoardKinds);
        _reducer = new AppReducer(keymap);
        _shutdownToken = _shutdown.Token;
        _network = new NetworkRunner(_connections, this, _shutdownToken);
        _effects = new EffectRunner(
            profiles,
            _connections,
            _network,
            this,
            _shutdownToken,
            clipboard: CreateClipboard());
        profiles.Caches.WriteFailed += OnCacheProblem;
        profiles.Caches.ReadProblem += OnCacheProblem;

        _connections.RateLimitChanged += OnRateLimitChanged;
        _dragScroller.Stepped += OnDragScrollStep;
    }

    public void Dispose()
    {
        _profiles.Caches.WriteFailed -= OnCacheProblem;
        _profiles.Caches.ReadProblem -= OnCacheProblem;

        _connections.RateLimitChanged -= OnRateLimitChanged;
        _tickTimer?.Dispose();
        _whichKeyTimer?.Dispose();
        _dragScroller.Stepped -= OnDragScrollStep;
        _dragScroller.Dispose();
        _profiles.Caches.Dispose();
        _effects.Dispose();
        _network.Dispose();
        _connections.Dispose();
        _shutdown.Dispose();
    }

    public int Run(StartOptions? start = null)
    {
        _state = LoadProfiles();

        if (start is not null)
        {
            _state = StartupReducer.WithOptions(_state, start);
        }

        _state = StatusMessages.KeymapStartup(_state, _keymapLoad);
        DetectBackground();

        try
        {
            OpenApplication();

            using var screen = new ShellScreen(
                _keymap,
                _colors,
                _application!.Screen.Size,
                StartTheme(),
                _glyphs);

            _screen = screen;
            screen.BodyRowsChanged += OnBodyRowsChanged;
            screen.Render(_state, TimeProvider.System.GetUtcNow());
            Dispatch(new ViewportChanged(screen.BodyRows, screen.BodyColumns, screen.BodyHeight));
            Dispatch(new ConnectionCheckRequested());
            Dispatch(new StartupRequested());
            _tickTimer = TimeProvider.System.CreateTimer(PostTick, null, _tickInterval, _tickInterval);

            RunLoop(screen);
        }
        finally
        {
            _uiThread.Detach();
            _application?.Dispose();
        }

        _shutdown.Cancel();
        _profiles.Caches.FlushAll();
        _effects.FlushTabs();

        return 0;
    }

    /// <summary>Creates, initialises and wires a Terminal.Gui application; also used again after an outside editor.</summary>
    private void OpenApplication()
    {
        var application = Application.Create();

        _application = application;
        _uiThread.Attach(application);
        application.Init();
        application.Keyboard.KeyBindings.Clear();

        var driver = application.Driver!;

        _colors = ConsoleCapabilities.LimitedByDriver(
            _colors,
            driver is { SupportsTrueColor: true, Force16Colors: false });

        if (_colors == ColorSupport.Basic16)
        {
            driver.Force16Colors = true;
        }

        application.Keyboard.KeyDown += OnKeyDown;
        application.Paste += OnPaste;
        application.Mouse.MouseEvent += OnMouse;
        application.Iteration += OnIteration;
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

        if (_effects.HasRunningWrites)
        {
            _state = _state with { Message = "Waiting for running writes to finish (up to 5 s) ...", Status = null };
            _screen!.Render(_state, TimeProvider.System.GetUtcNow());

            await _effects.WaitForWritesAsync(_quitWait, CancellationToken.None);
        }

        _application!.Invoke(_application.RequestStop);
    }

    /// <summary>Thread-safe entry for background work: runs the reducer on the UI thread.</summary>
    public void Post(AppCommand command)
    {
        // Ticks are not held while an outside editor runs: they would pile up, stale, for a long editing session.
        if (_shutdownToken.IsCancellationRequested || (command is Tick && _held.IsPaused))
        {
            return;
        }

        InvokeOnUi(() => Dispatch(command));
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

    /// <summary>
    /// Terminal.Gui sets "cursor needs update" on every focus change and never clears it, so each main-loop iteration
    /// (25 a second) hid the cursor again and moved it home (<c>ESC[1;1H ESC[?25l</c>): an idle screen was never quiet
    /// (#744). The shell never shows the hardware cursor (the driver hides it at start), so there is nothing to update.
    /// </summary>
    private void OnIteration(object? sender, EventArgs<IApplication?> eventArgs)
    {
        _application?.Driver?.SetCursorNeedsUpdate(false);
    }

    private void OnKeyDown(object? sender, Key key)
    {
        // Keys typed after the outside editor was asked for belong to nobody: the loop is stopping.
        if (_pendingEdit is not null)
        {
            key.Handled = true;

            return;
        }

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
            else if (effect is EditExternallyEffect edit)
            {
                AskForExternalEdit(edit);
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
                _effects.Run(effect, _state.Profiles.Active);
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
            _ = _effects.StartConnectionCheckAsync(check);
        }
    }

    private IClipboard CreateClipboard()
    {
        // The OSC 52 fallback writes to the terminal, so it goes through the UI thread, the one that draws frames.
        TextWriter? terminal = Console.IsOutputRedirected ? null : new TerminalWriter(Console.Out, _uiThread);

        return Clipboards.Create(
            ApplicationDirectories.DetectOperatingSystem(),
            EnvironmentReader.ForProcess(),
            new ProcessRunner(),
            terminal);
    }
}
