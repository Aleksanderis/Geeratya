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
    public void GivenThemeName_WhenSubmittingThePalette_ThenTheHostIsAskedAndNothingIsSavedYet()
    {
        var result = Run("theme light");

        Assert.Equal("light", Assert.IsType<ApplyThemeEffect>(Assert.Single(result.Effects)).Name);
        Assert.Equal(AppSettings.Default, result.State.Settings);
    }

    [Fact]
    public void GivenNoName_WhenSubmittingTheThemeCommand_ThenTheHostIsAskedToList()
    {
        var result = Run("theme");

        Assert.Equal(string.Empty, Assert.IsType<ApplyThemeEffect>(Assert.Single(result.Effects)).Name);
    }

    [Fact]
    public void GivenTwoNames_WhenSubmittingTheThemeCommand_ThenUsageShows()
    {
        var result = Run("theme light dark");

        Assert.Empty(result.Effects);
        Assert.Equal(SettingsReducer.ThemeUsage, result.State.Message);
    }

    [Fact]
    public void GivenAppliedTheme_WhenTheHostAnswers_ThenItIsSavedAsTheChoice()
    {
        var result = _reducer.Reduce(AppState.Initial, new ThemeApplied("light", true, "Theme light."));

        Assert.Equal("light", result.State.Settings.Theme);
        Assert.Equal("light", Assert.IsType<SaveSettingsEffect>(Assert.Single(result.Effects)).Settings.Theme);
        Assert.Equal("Theme light.", result.State.Message);
    }

    [Fact]
    public void GivenRefusedTheme_WhenTheHostAnswers_ThenTheChoiceStaysAndTheReasonShows()
    {
        var result = _reducer.Reduce(AppState.Initial, new ThemeApplied("bad", false, "Theme bad not used: x."));

        Assert.Equal(AppSettings.DefaultTheme, result.State.Settings.Theme);
        Assert.Empty(result.Effects);
        Assert.Equal("Theme bad not used: x.", result.State.Message);
    }

    [Fact]
    public void GivenAThemeInTheFile_WhenLoading_ThenItComesBack()
    {
        var store = new SettingsStore(_directory);

        store.Save(new AppSettings(Theme: "light"));

        Assert.Equal("light", store.Load().Theme);
    }

    [Fact]
    public void GivenAnOldFileWithoutATheme_WhenLoading_ThenTheThemeIsDark()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, SettingsStore.FileName), """{ "statusHints": false }""");

        var settings = new SettingsStore(_directory).Load();

        Assert.Equal("dark", settings.Theme);
        Assert.False(settings.StatusHints);
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
