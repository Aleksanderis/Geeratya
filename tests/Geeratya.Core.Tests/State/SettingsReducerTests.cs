using Geeratya.Core.Keymap;
using Geeratya.Core.Settings;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.State;

public sealed class SettingsReducerTests : IDisposable // Dispose removes the temporary settings folder
{
    private readonly AppReducer _reducer = new(DefaultKeymap.Load());
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-settings-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("hints off", false, true)]
    [InlineData("whichkey off", true, false)]
    public void GivenSetOff_WhenSubmittingThePalette_ThenTheSwitchFlipsAndTheFileIsSaved(
        string text,
        bool hints,
        bool whichKey)
    {
        var result = Run($"set {text}");
        var expected = new AppSettings(hints, whichKey);

        Assert.Equal(expected, result.State.Settings);
        Assert.Equal(expected, Assert.IsType<SaveSettingsEffect>(Assert.Single(result.Effects)).Settings);
    }

    [Theory]
    [InlineData("set hints maybe")]
    [InlineData("set colour on")]
    [InlineData("set hints")]
    public void GivenBadArguments_WhenSubmittingThePalette_ThenUsageShowsAndNothingChanges(string text)
    {
        var result = Run(text);

        Assert.Equal(AppSettings.Default, result.State.Settings);
        Assert.Empty(result.Effects);
        Assert.Contains("Usage", result.State.Message);
    }

    [Fact]
    public void GivenSavedSettings_WhenLoading_ThenTheyComeBack_AndABadFileGivesTheDefaults()
    {
        var store = new SettingsStore(_directory);

        Assert.Equal(AppSettings.Default, store.Load());

        store.Save(new AppSettings(StatusHints: false));

        Assert.Equal(new AppSettings(StatusHints: false), store.Load());

        File.WriteAllText(Path.Combine(_directory, SettingsStore.FileName), "{ not json");

        Assert.Equal(AppSettings.Default, store.Load());
    }

    private ReduceResult Run(string text)
    {
        var state = AppState.Initial with { Mode = InputMode.Command, PaletteText = text };

        return _reducer.Reduce(state, new KeyPressed("Enter"));
    }
}
