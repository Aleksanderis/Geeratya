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
        var settings = new AppSettings(false, true);

        var outcome = new SettingsEffectHandler(store).Run(new SaveSettingsEffect(settings));

        Assert.Null(outcome);
        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void GivenAnUnwritableDirectory_WhenRunning_ThenTheFailureIsReported()
    {
        Directory.CreateDirectory(_directory);

        var blocker = Path.Combine(_directory, "blocker");

        File.WriteAllText(blocker, "a file where the configuration directory should be");

        var handler = new SettingsEffectHandler(new SettingsStore(blocker));

        var outcome = handler.Run(new SaveSettingsEffect(AppSettings.Default));

        Assert.Contains("settings.json", Assert.IsType<ProfileOperationFailed>(outcome).Message);
    }

    [Fact]
    public void GivenAnotherEffect_WhenRunning_ThenNothingHappens()
    {
        Assert.Null(new SettingsEffectHandler(new SettingsStore(_directory)).Run(new QuitEffect()));
        Assert.False(Directory.Exists(_directory));
    }
}
