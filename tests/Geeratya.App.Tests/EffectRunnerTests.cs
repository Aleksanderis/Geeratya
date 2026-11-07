using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.BoardKinds;
using Geeratya.Core.Cache;
using Geeratya.Core.History;
using Geeratya.Core.Profiles;
using Geeratya.Core.Comments;
using Geeratya.Core.SavedViews;
using Geeratya.Core.Settings;
using Geeratya.Core.Startup;
using Geeratya.Core.Clipboard;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Platform;
using Geeratya.Platform.Browser;
using Geeratya.Platform.Clipboard;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

public sealed class EffectRunnerTests : IDisposable
{
    /// <summary>The effects the shell itself performs: they touch the terminal or are ordered around the connection.</summary>
    private static readonly Type[] _terminalBound =
    [
        typeof(QuitEffect),
        typeof(ReloadKeymapEffect),
        typeof(ApplyThemeEffect),
        typeof(CheckConnectionEffect),
        typeof(EditExternallyEffect),
    ];

    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("geeratya-effects-");
    private readonly List<Command> _posted = [];
    private readonly FakeBrowserLauncher _launcher = new(BrowserLaunchResult.Success);
    private readonly FakeClipboard _clipboard = new(ClipboardResult.Success("wl-copy"));
    private readonly ProfileServices _profiles;
    private readonly SiteConnections _connections;
    private readonly NetworkRunner _network;
    private readonly EffectRunner _runner;

    public EffectRunnerTests()
    {
        var state = Path.Combine(_directory.FullName, "state");

        _profiles = new ProfileServices(
            new ProfileStore(_directory.FullName),
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)),
            new SiteCaches(Path.Combine(_directory.FullName, "cache")),
            new HistoryStore(state),
            new BoardKindStore(state),
            Settings: new SettingsStore(Path.Combine(_directory.FullName, "settings.json")),
            SavedViews: new SavedViewStore(state),
            Tabs: new RememberedTabsStore(state));

        var sink = new ListCommandSink(_posted);

        _connections = new SiteConnections(_profiles.Tokens, boardKinds: _profiles.BoardKinds);
        _network = new NetworkRunner(_connections, sink, CancellationToken.None);
        _runner = new EffectRunner(
            _profiles,
            _connections,
            _network,
            sink,
            CancellationToken.None,
            _launcher,
            _clipboard);
    }

    [Fact]
    public void GivenEveryConcreteEffectType_WhenRouting_ThenEachHasARouteOrIsTerminalBound()
    {
        var unrouted = EffectTypes()
            .Where(type => !_terminalBound.Contains(type))
            .Where(type => !_runner.IsRouted(Instance(type)))
            .Select(type => type.Name)
            .ToList();

        Assert.True(
            unrouted.Count == 0,
            $"Effect types without a route in EffectRunner (add one, or list them as terminal-bound): "
            + string.Join(", ", unrouted));
    }

    [Fact]
    public void GivenTerminalBoundEffects_WhenRouting_ThenTheRunnerLeavesThemToTheShell()
    {
        foreach (var type in _terminalBound)
        {
            Assert.False(_runner.IsRouted(Instance(type)), type.Name);
        }
    }

    [Fact]
    public async Task GivenOpenUrlEffect_WhenRunning_ThenTheBrowserOpensItAndTheReplyIsPosted()
    {
        var url = new Uri("https://work.atlassian.net/browse/ABC-1");

        _runner.Run(new OpenUrlEffect(url, "ABC-1"), active: null);

        await WaitForPostAsync();

        Assert.Equal(url, Assert.Single(_launcher.Opened));
        Assert.Equal("ABC-1", Assert.IsType<UrlOpenFinished>(Assert.Single(_posted)).Label);
    }

    [Fact]
    public async Task GivenCopyEffect_WhenRunning_ThenTheClipboardGetsTheTextAndTheReplyIsPosted()
    {
        _runner.Run(new CopyToClipboardEffect("some text", "1 line"), active: null);

        await WaitForPostAsync();

        Assert.Equal("some text", Assert.Single(_clipboard.Copied));
        Assert.Equal(new ClipboardCopied("1 line", true, "wl-copy"), Assert.Single(_posted));
    }

    [Fact]
    public void GivenCacheEffectWithoutActiveProfile_WhenRunning_ThenNothingHappens()
    {
        _runner.Run(new InvalidateCacheEffect([]), active: null);

        Assert.Empty(_posted);
    }

    public void Dispose()
    {
        _runner.Dispose();
        _network.Dispose();
        _connections.Dispose();
        _profiles.Caches.Dispose();
        _directory.Delete(recursive: true);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test helper, never trimmed.")]
    private static IEnumerable<Type> EffectTypes()
    {
        return typeof(Effect).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(Effect)) && !type.IsAbstract);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Test helper, never trimmed.")]
    private static Effect Instance(Type type)
    {
        return (Effect)RuntimeHelpers.GetUninitializedObject(type);
    }

    private async Task WaitForPostAsync()
    {
        for (var attempt = 0; attempt < 200 && Posted() == 0; attempt++)
        {
            await Task.Delay(10);
        }
    }

    private int Posted()
    {
        lock (_posted)
        {
            return _posted.Count;
        }
    }
}
