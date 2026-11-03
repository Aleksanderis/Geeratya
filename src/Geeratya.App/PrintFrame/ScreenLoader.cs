using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.PrintFrame;

/// <summary>
/// Opens a source of a profile in a headless session (reducer plus the Jira load handlers, no terminal) and returns
/// the loaded state a frame is drawn from. Reads only, with a placeholder token: it never touches the secret store.
/// </summary>
public sealed class ScreenLoader
{
    private readonly KeymapTable _keymap;
    private readonly ITransportFactory _transports;

    public ScreenLoader(KeymapTable keymap, ITransportFactory transports)
    {
        _keymap = keymap;
        _transports = transports;
    }

    /// <summary>Opens <paramref name="source"/> and returns the loaded state, sized to <paramref name="viewport"/>.</summary>
    public async Task<AppState> LoadAsync(
        Profile profile,
        AppSettings settings,
        ScreenSource source,
        ViewLayout? layout,
        ViewportChanged viewport,
        CancellationToken cancellationToken)
    {
        var tokens = new TokenProvider(new FixedSecretStore(), new EnvironmentReader(_ => null));

        using var connections = new SiteConnections(tokens, _transports);
        using var session = new HeadlessSession(new AppReducer(_keymap), connections, cancellationToken);

        var failure = await connections.ActivateAsync(profile, 1, cancellationToken);
        var state = AppState.Initial with
        {
            Settings = settings,
            Profiles = ProfileCatalog.Empty.With(profile),
            Session = SiteSession.Empty(profile),
        };

        if (failure is not null)
        {
            state = await session.RunAsync(state, failure);
        }

        state = await session.RunAsync(state, viewport);
        state = await session.RunAsync(
            state with { Mode = InputMode.Command, PaletteText = source.PaletteText() },
            new ActionRequested(ActionIds.PaletteSubmit));

        if (source.Kind == ScreenSourceKind.Issue)
        {
            state = await session.RunAsync(state, new ActionRequested(ActionIds.ItemOpen));
        }

        return await ApplyLayoutAsync(session, state, layout);
    }

    private static async Task<AppState> ApplyLayoutAsync(HeadlessSession session, AppState state, ViewLayout? layout)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        if (layout is null || tab is null || tab.Definition.Layout == layout)
        {
            return state;
        }

        return await session.RunAsync(state, new ActionRequested(ActionIds.ViewLayoutToggle));
    }
}
