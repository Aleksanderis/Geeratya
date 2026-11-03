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
    [InlineData("hints off", HintLevel.Off, true)]
    [InlineData("whichkey off", HintLevel.Full, false)]
    public void GivenSetOff_WhenSubmittingThePalette_ThenTheSwitchFlipsAndTheFileIsSaved(
        string text,
        HintLevel hints,
        bool whichKey)
    {
        var result = Run($"set {text}");
        var expected = new AppSettings(hints, whichKey);

        Assert.Equal(expected, result.State.Settings);
        Assert.Equal(expected, Assert.IsType<SaveSettingsEffect>(Assert.Single(result.Effects)).Settings);
    }

    [Fact]
    public void GivenAnUnknownSetting_WhenSubmittingThePalette_ThenUsageShowsAndNothingChanges()
    {
        var result = Run("set colour on");

        Assert.Equal(AppSettings.Default, result.State.Settings);
        Assert.Empty(result.Effects);
        Assert.Equal(SettingsReducer.Usage, result.State.Message);
    }

    [Fact]
    public void GivenABadValue_WhenSubmittingThePalette_ThenTheAcceptedValuesShowAndNothingChanges()
    {
        var result = Run("set hints maybe");

        Assert.Empty(result.Effects);
        Assert.Equal("hints takes full, minimal or off.", result.State.Message);
    }

    [Fact]
    public void GivenNoArgument_WhenSubmittingThePalette_ThenEverySettingShowsWithItsDefault()
    {
        var message = Run("set").State.Message;

        foreach (var setting in SettingsRegistry.All)
        {
            Assert.Contains($"{setting.Name} {setting.Default} (default {setting.Default})", message);
        }
    }

    [Fact]
    public void GivenOneName_WhenSubmittingThePalette_ThenItsValueDefaultAndHelpShow()
    {
        var message = Run("set whichkey").State.Message;

        Assert.Contains("whichkey on (default on; on or off)", message);
        Assert.Contains(SettingsRegistry.Find("whichkey")!.Help, message);
    }

    [Fact]
    public void GivenSetTheme_WhenSubmittingThePalette_ThenTheHostIsAsked()
    {
        var result = Run("set theme nord");

        Assert.Equal("nord", Assert.IsType<ApplyThemeEffect>(Assert.Single(result.Effects)).Name);
    }

    [Fact]
    public void GivenSetMinimal_WhenSubmittingThePalette_ThenTheHintLevelIsMinimalAndSaved()
    {
        var result = Run("set hints minimal");

        Assert.Equal(HintLevel.Minimal, result.State.Settings.StatusHints);
        Assert.Equal("\"minimal\"", SettingsRegistry.ToJson(SettingsRegistry.Find("hints")!, result.State.Settings));
    }

    [Fact]
    public void GivenAReloadedFile_WhenTheHostAnswers_ThenItsValuesApplyAndTheMessageShows()
    {
        var result = _reducer.Reduce(
            AppState.Initial,
            new SettingsReloaded(new AppSettings(StatusHints: HintLevel.Off), "changed"));

        Assert.Equal(HintLevel.Off, result.State.Settings.StatusHints);
        Assert.Equal("changed", result.State.Message);
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
    public void GivenAThemeWrittenToTheFile_WhenLoading_ThenItComesBack()
    {
        var store = new SettingsStore(_directory);

        store.Write(SettingsRegistry.Find("theme")!, new AppSettings(Theme: "light"));

        Assert.Equal("light", store.Load().Theme);
    }

    [Fact]
    public void GivenAnOldFileWithoutATheme_WhenLoading_ThenTheDefaultThemeIsUsed()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, SettingsStore.FileName), """{ "statusHints": false }""");

        var settings = new SettingsStore(_directory).Load();

        Assert.Equal(AppSettings.DefaultTheme, settings.Theme);
        Assert.Equal(HintLevel.Off, settings.StatusHints);
    }

    private ReduceResult Run(string text)
    {
        var state = AppState.Initial with { Mode = InputMode.Command, PaletteText = text };

        return _reducer.Reduce(state, new KeyPressed("Enter"));
    }
}
