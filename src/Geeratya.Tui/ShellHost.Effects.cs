using Geeratya.Core.Cache;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering.Themes;

using AppCommand = Geeratya.Core.State.Command;

namespace Geeratya.Tui;

public sealed partial class ShellHost
{
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
