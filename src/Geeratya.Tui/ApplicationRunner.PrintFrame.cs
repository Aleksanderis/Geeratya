using Geeratya.App;
using Geeratya.Core;
using Geeratya.App.PrintFrame;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui;

/// <summary>
/// <c>--print-frame</c>: the frame as plain text, with ANSI colours, or of a real screen from a profile.
/// </summary>
public sealed partial class ApplicationRunner
{
    private static readonly TimeSpan _screenTimeout = TimeSpan.FromSeconds(30);

    private int PrintFrame(string[] arguments, KeymapTable keymap)
    {
        var size = FrameOptions.ParseSize(ValueOf(arguments, SizeFlag));
        var mode = FrameOptions.ParseMode(ValueOf(arguments, ModeFlag));
        var scroll = FrameOptions.ParseScroll(ValueOf(arguments, HelpScrollFlag));
        var page = FrameOptions.ParseScroll(ValueOf(arguments, HelpPageFlag));
        var level = FrameOptions.ParseLevel(ValueOf(arguments, LevelFlag));
        var colorsText = ValueOf(arguments, ColorsFlag);
        var colors = colorsText is null ? null : FrameOptions.ParseColors(colorsText);
        var layoutText = ValueOf(arguments, LayoutFlag);
        var layout = FrameOptions.ParseLayout(layoutText);
        var sourceText = ValueOf(arguments, SourceFlag);
        var source = sourceText is null ? null : ScreenSource.Parse(sourceText);

        if (size is null || mode is null || scroll is null || page is null || level is null
            || (colorsText is not null && colors is null)
            || (layoutText is not null && layout is null)
            || (sourceText is not null && source is null))
        {
            _error.WriteLine(Usage);

            return 2;
        }

        var glyphs = arguments.Contains(AsciiFlag) ? GlyphSet.Ascii : _glyphs;
        var state = FrameOptions.SampleState(mode.Value);

        state = state with
        {
            HelpScroll = scroll.Value,
            HelpPage = page.Value,
            Settings = state.Settings with { CommandLevel = level.Value },
        };

        var services = source is null && colors is null ? null : Profiles();

        if (source is not null)
        {
            var loaded = LoadScreen(arguments, services, source, layout, size);

            if (loaded is null)
            {
                return 1;
            }

            state = loaded;
        }

        var caps = colors is { } shown && shown != ColorSupport.None && SettingsOf(services).LabelCaps;
        var frame = FrameBuilder.BuildStyled(state, size.Width, size.Height, keymap, null, null, glyphs, caps);

        var light = arguments.Contains(LightFlag);

        foreach (var line in Lines(frame, colors, light, SettingsOf(services), services, glyphs))
        {
            _output.WriteLine(line);
        }

        return 0;
    }

    private static IEnumerable<string> Lines(
        StyledFrame? frame,
        ColorSupport? colors,
        bool light,
        AppSettings settings,
        ProfileServices? services,
        GlyphSet glyphs)
    {
        if (frame is null)
        {
            return [];
        }

        if (colors is not { } tier)
        {
            return frame.PlainLines();
        }

        var catalog = new ThemeCatalog(services?.ThemesDirectory);
        var name = light && string.IsNullOrWhiteSpace(settings.Theme) ? BuiltInThemes.AutoName : settings.Theme;
        var theme = new ThemeSwitcher(catalog)
            .Start(name, light)
            .Theme
            .WithTerminalBackground(settings.TransparentBackground);

        return AnsiFrameWriter.Write(frame, theme, tier, glyphs != GlyphSet.Ascii);
    }

    private ProfileServices? Profiles()
    {
        try
        {
            return _profileFactory.Create();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private AppState? LoadScreen(
        string[] arguments,
        ProfileServices? services,
        ScreenSource source,
        ViewLayout? layout,
        FrameSize size)
    {
        var profile = FindProfile(services, ValueOf(arguments, ProfileFlag));

        if (profile is null)
        {
            return null;
        }

        var allowed = JiraHostAllowList.Parse(_environment.Get(JiraHostAllowList.EnvironmentVariable));

        if (!Uri.TryCreate(profile.SiteUrl, UriKind.Absolute, out var site) || !allowed.Contains(site.Host))
        {
            _error.WriteLine(
                $"{ApplicationInfo.Name}: {profile.SiteUrl} is not in {JiraHostAllowList.EnvironmentVariable}, "
                + "so the screen was not loaded (the token is a placeholder: use a fake Jira).");

            return null;
        }

        var loader = new ScreenLoader(DefaultKeymap.Load(), _transports ?? new AllowListedTransportFactory(allowed));
        using var timeout = new CancellationTokenSource(_screenTimeout);

        var viewport = new ViewportChanged(
            FrameBuilder.ListRows(size.Height),
            size.Width,
            FrameBuilder.BodyHeight(size.Height));

        var state = loader.LoadAsync(profile, SettingsOf(services), source, layout, viewport, timeout.Token)
            .GetAwaiter()
            .GetResult();

        return Failure(state) is { } problem ? Refuse(problem) : state;
    }

    private AppState? Refuse(string problem)
    {
        _error.WriteLine($"{ApplicationInfo.Name}: {problem}");

        return null;
    }

    // The loaded screen is only worth drawing when the source opened and its load ended; otherwise scripts see exit 1.
    private static string? Failure(AppState state)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        return tab?.Load switch
        {
            null => $"the source did not open ({state.Message ?? "no tab"}).",
            Loading => "the load did not finish in time.",
            Failed failed => $"the load failed: {failed.Message}",
            _ => null,
        };
    }

    private Profile? FindProfile(ProfileServices? services, string? name)
    {
        if (services is null)
        {
            _error.WriteLine($"{ApplicationInfo.Name}: no home directory found, so there are no profiles.");

            return null;
        }

        var catalog = services.Store.Load().Catalog;
        var profile = name is null ? catalog.Active : catalog.Find(name);

        if (profile is null)
        {
            var known = string.Join(", ", catalog.Profiles.Select(candidate => candidate.Name));

            _error.WriteLine($"{ApplicationInfo.Name}: no profile {(name is null ? "is active" : $"named '{name}'")} "
                + $"(saved: {(known.Length == 0 ? "none" : known)}); choose one with {ProfileFlag}.");
        }

        return profile;
    }

    private static AppSettings SettingsOf(ProfileServices? services)
    {
        try
        {
            return services?.Settings?.Load() ?? AppSettings.Default;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }
    }
}
