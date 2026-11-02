using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
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
public sealed class ShellHost : IDisposable
{
    private const string CorruptNotice =
        "profiles.json could not be read and was ignored; a copy was kept as profiles.json.corrupt.";

    private readonly KeymapTable _keymap;
    private readonly ColorSupport _colors;
    private readonly AppReducer _reducer;
    private readonly ProfileServices _profiles;
    private readonly ProfileEffectHandler _profileEffects;
    private readonly BrowserEffectHandler _browserEffects;
    private readonly SiteConnections _connections;
    private NetworkRunner? _network;
    private ViewLoadHandler? _viewLoads;
    private SourceLoadHandler? _sourceLoads;
    private ConnectionCheckHandler? _connectionChecks;
    private IApplication? _application;
    private ShellScreen? _screen;
    private CancellationToken _shutdownToken;
    private AppState _state = AppState.Initial;

    public ShellHost(KeymapTable keymap, ColorSupport colors, ProfileServices profiles)
    {
        _keymap = keymap;
        _colors = colors;
        _profiles = profiles;
        _profileEffects = new ProfileEffectHandler(profiles);
        _browserEffects = new BrowserEffectHandler(
            BrowserLaunchers.Create(ApplicationDirectories.DetectOperatingSystem(), new ProcessRunner()));
        _connections = new SiteConnections(profiles.Tokens);
        _reducer = new AppReducer(keymap);
        profiles.Caches.WriteFailed += OnCacheWriteFailed;
    }

    public void Dispose()
    {
        _profiles.Caches.WriteFailed -= OnCacheWriteFailed;
        _profiles.Caches.Dispose();
        _network?.Dispose();
        _connections.Dispose();
    }

    public int Run()
    {
        using var application = Application.Create();

        using var shutdown = new CancellationTokenSource();

        _shutdownToken = shutdown.Token;
        _state = LoadProfiles();
        _network = new NetworkRunner(_connections, Post, _shutdownToken);
        _viewLoads = new ViewLoadHandler(_network);
        _sourceLoads = new SourceLoadHandler(_network);
        _connectionChecks = new ConnectionCheckHandler(_connections, _network);

        _application = application;
        application.Init();
        application.Keyboard.KeyBindings.Clear();

        if (_colors == ColorSupport.Basic16)
        {
            application.Driver!.Force16Colors = true;
        }

        application.Keyboard.KeyDown += OnKeyDown;
        application.Paste += OnPaste;

        using var screen = new ShellScreen(_keymap, _colors, application.Screen.Size);

        _screen = screen;
        screen.BodyRowsChanged += rows => Dispatch(new ViewportChanged(rows));
        screen.Render(_state);
        Dispatch(new ViewportChanged(screen.BodyRows));
        Dispatch(new ConnectionCheckRequested());
        application.Run(screen);
        shutdown.Cancel();
        _profiles.Caches.FlushAll();

        return 0;
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

    private void OnKeyDown(object? sender, Key key)
    {
        var spelled = KeyTranslator.Translate(key);

        if (spelled is null)
        {
            return;
        }

        key.Handled = true;

        Dispatch(new KeyPressed(spelled));
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

        _state = result.State;
        _screen!.Render(_state);

        var checks = new List<CheckConnectionEffect>();

        foreach (var effect in result.Effects)
        {
            if (effect is QuitEffect)
            {
                _application!.RequestStop();
            }
            else if (effect is DropSiteCacheEffect drop)
            {
                _network!.CancelAll();
                _profiles.Caches.Drop(drop.Profile);
                _connections.Drop(drop.Profile);
            }
            else if (effect is CancelTabEffect cancel)
            {
                _network!.CancelScope(cancel.Tab.Value);
            }
            else if (effect is LoadViewPageEffect load)
            {
                _ = _viewLoads!.StartAsync(load, ActiveCache());
            }
            else if (effect is LoadSourcesEffect sources)
            {
                _ = _sourceLoads!.StartAsync(sources);
            }
            else if (effect is WriteCacheEffect write)
            {
                ActiveCache()?.Put(write.Key, write.View);
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

        // After ConnectAsync, so the check waits for the connection the same dispatch asked for.
        foreach (var check in checks)
        {
            _ = _connectionChecks!.StartAsync(check);
        }
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
        return await _profileEffects.RunAsync(effect, _shutdownToken)
            ?? await _browserEffects.RunAsync(effect, _shutdownToken);
    }

    private AppState LoadProfiles()
    {
        try
        {
            var loaded = _profiles.Store.Load();
            var notice = loaded.Status == ProfileLoadStatus.Corrupt ? CorruptNotice : null;

            return AppState.Initial with
            {
                Profiles = loaded.Catalog,
                Session = SiteSession.Empty(loaded.Catalog.Active),
                Message = notice,
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AppState.Initial with { Message = $"Could not read the profiles file: {exception.Message} (adding a profile would replace it)" };
        }
    }
}
