using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
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
    private readonly SiteConnections _connections;
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
        _connections = new SiteConnections(profiles.Tokens);
        _reducer = new AppReducer(keymap);
    }

    public void Dispose()
    {
        _connections.Dispose();
    }

    public int Run()
    {
        using var application = Application.Create();

        using var shutdown = new CancellationTokenSource();

        _shutdownToken = shutdown.Token;
        _state = LoadProfiles();

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
        screen.Render(_state);
        _ = ConnectAsync();
        application.Run(screen);
        shutdown.Cancel();

        return 0;
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

        foreach (var effect in result.Effects)
        {
            if (effect is QuitEffect)
            {
                _application!.RequestStop();
            }
            else if (effect is DropSiteCacheEffect drop)
            {
                _connections.Drop(drop.Profile);
            }
            else
            {
                _ = RunEffectAsync(effect);
            }
        }

        _ = ConnectAsync();
    }

    /// <summary>Builds the connection for the active profile off the UI thread; no-op when already built.</summary>
    private async Task ConnectAsync()
    {
        var failure = await _connections.ActivateAsync(_state.Profiles.Active, _shutdownToken);

        if (failure is not null && !_shutdownToken.IsCancellationRequested)
        {
            _application!.Invoke(() => Dispatch(failure));
        }
    }

    /// <summary>Runs a profile effect off the UI thread and feeds its outcome back as a command.</summary>
    private async Task RunEffectAsync(Effect effect)
    {
        var outcome = await Task.Run(() => _profileEffects.RunAsync(effect, _shutdownToken));

        if (outcome is not null && !_shutdownToken.IsCancellationRequested)
        {
            _application!.Invoke(() => Dispatch(outcome));
        }
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
