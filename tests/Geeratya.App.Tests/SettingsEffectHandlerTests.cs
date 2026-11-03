using Geeratya.Core.Keymap;
using Geeratya.Core.Settings;
using Geeratya.Core.State;

namespace Geeratya.App.Tests;

public sealed class SettingsEffectHandlerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-settings-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void GivenSaveEffect_WhenRunning_ThenTheFileHoldsTheSettingsAndNothingIsReported()
    {
        var store = new SettingsStore(_directory);
        var settings = new AppSettings(StatusHints: HintLevel.Off);

        var outcome = new SettingsEffectHandler(store)
            .Run(new SaveSettingsEffect(SettingsRegistry.Find("hints")!, settings));

        Assert.Null(outcome);
        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void GivenTheSettingsScreen_WhenChangingAToggleAndAHintOption_ThenBothAreSavedAndLoadAfterARestart()
    {
        var reducer = new AppReducer(DefaultKeymap.Load());
        var handler = new SettingsEffectHandler(new SettingsStore(_directory));
        var state = reducer.Reduce(AppState.Initial, new ActionRequested(ActionIds.SettingsOpen, 1, default)).State;

        foreach (var name in new[] { "hints.whichKey", "startup.restore" })
        {
            var rows = SettingsRows.Build(state).ToList();
            var opened = state with { SettingsScreen = new SettingsScreenState(
                rows.FindIndex(row => row.Setting?.Name == name)) };
            var result = reducer.Reduce(opened, new KeyPressed("Enter"));

            state = result.State;

            Assert.All(result.Effects, effect => Assert.Null(handler.Run(effect)));
        }

        var restarted = new SettingsStore(_directory).Load();

        Assert.False(state.Settings.WhichKey);
        Assert.Equal(state.Settings, restarted);
        Assert.False(restarted.RestoreTabs);
    }

    [Fact]
    public void GivenAnUnwritableDirectory_WhenRunning_ThenTheFailureIsReported()
    {
        Directory.CreateDirectory(_directory);

        var blocker = Path.Combine(_directory, "blocker");

        File.WriteAllText(blocker, "a file where the configuration directory should be");

        var handler = new SettingsEffectHandler(new SettingsStore(blocker));

        var outcome = handler.Run(new SaveSettingsEffect(SettingsRegistry.Find("hints")!, AppSettings.Default));

        Assert.Contains("settings.json", Assert.IsType<ProfileOperationFailed>(outcome).Message);
    }

    [Fact]
    public void GivenAFileChangedSinceItWasRead_WhenRunning_ThenTheNewValuesComeBackAndNothingIsWritten()
    {
        var store = new SettingsStore(_directory);
        var path = Path.Combine(_directory, SettingsStore.FileName);

        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, """{ "whichKey": true }""");
        store.Read();
        File.WriteAllText(path, """{ "whichKey": false }""");

        var outcome = new SettingsEffectHandler(store)
            .Run(new SaveSettingsEffect(SettingsRegistry.Find("hints")!, new AppSettings(StatusHints: HintLevel.Off)));

        var reloaded = Assert.IsType<SettingsReloaded>(outcome);

        Assert.False(reloaded.Settings.WhichKey);
        Assert.Equal(HintLevel.Full, reloaded.Settings.StatusHints);
        Assert.Equal("""{ "whichKey": false }""", File.ReadAllText(path));
    }

    [Fact]
    public void GivenAFileThatDoesNotParse_WhenRunning_ThenTheWriteIsRefusedAndTheFileStays()
    {
        Directory.CreateDirectory(_directory);

        var path = Path.Combine(_directory, SettingsStore.FileName);

        File.WriteAllText(path, "{ not json");

        var outcome = new SettingsEffectHandler(new SettingsStore(_directory))
            .Run(new SaveSettingsEffect(SettingsRegistry.Find("hints")!, AppSettings.Default));

        Assert.Contains("not valid JSON", Assert.IsType<ProfileOperationFailed>(outcome).Message);
        Assert.Equal("{ not json", File.ReadAllText(path));
    }

    [Fact]
    public void GivenAnotherEffect_WhenRunning_ThenNothingHappens()
    {
        Assert.Null(new SettingsEffectHandler(new SettingsStore(_directory)).Run(new QuitEffect()));
        Assert.False(Directory.Exists(_directory));
    }
}
