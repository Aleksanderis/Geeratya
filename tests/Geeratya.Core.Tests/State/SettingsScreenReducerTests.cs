using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Settings;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.State;

public sealed class SettingsScreenReducerTests
{
    private static readonly string[] _themes = ["dark", "light", "nord"];

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenTheRegistry_WhenBuildingTheRows_ThenEverySettingHasOneRowAndTheExtrasFollow()
    {
        var rows = SettingsRows.Build(Opened().State);
        var settingRows = rows.Where(row => row.Kind == SettingsRowKind.Setting).Select(row => row.Setting!.Name);

        Assert.Equal(SettingsRegistry.All.Select(setting => setting.Name).Order(), settingRows.Order());
        Assert.Equal(
            [SettingsRowKind.Profiles, SettingsRowKind.Info, SettingsRowKind.Info, SettingsRowKind.Info],
            rows.TakeLast(4).Select(row => row.Kind));
        Assert.Equal(
            ["Appearance", "Hints and keys", "Startup", "Profiles", "About"],
            SettingsRows.Categories(rows));
    }

    [Fact]
    public void GivenThemeNames_WhenListingTheThemeChoices_ThenAutoComesFirst()
    {
        var theme = SettingsRegistry.Find(SettingsRegistry.ThemeName)!;

        Assert.Equal(["auto", .. _themes], SettingsRows.ChoicesOf(Opened().State, theme));
    }

    [Fact]
    public void GivenTheKey_WhenPressingItTwice_ThenTheScreenOpensAndCloses()
    {
        var opened = _reducer.Reduce(AppState.Initial, new ActionRequested(ActionIds.SettingsOpen, 1, default));
        var closed = _reducer.Reduce(opened.State, new ActionRequested(ActionIds.SettingsOpen, 1, default));

        Assert.NotNull(opened.State.SettingsScreen);
        Assert.Null(closed.State.SettingsScreen);
    }

    [Fact]
    public void GivenThePaletteCommand_WhenSubmittingSettings_ThenTheScreenOpens()
    {
        var typed = AppState.Initial with { Mode = InputMode.Command, PaletteText = "settings" };

        var result = _reducer.Reduce(typed, new KeyPressed("Enter"));

        Assert.NotNull(result.State.SettingsScreen);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenAToggleRow_WhenPressingEnter_ThenItFlipsAtOnceAndTheSaveIsAsked()
    {
        var state = SelectRow(Opened().State, "hints.whichKey");

        var result = Press(state, "Enter");

        var save = Assert.IsType<SaveSettingsEffect>(Assert.Single(result.Effects));

        Assert.False(result.State.Settings.WhichKey);
        Assert.Equal("hints.whichKey", save.Setting.Name);
        Assert.False(save.Settings.WhichKey);
    }

    [Fact]
    public void GivenAChoiceRow_WhenOpeningTheListAndTakingAWord_ThenTheValueChangesAndIsSaved()
    {
        var state = SelectRow(Opened().State, "hints.statusBar");
        var listed = Press(state, "Enter").State;
        var moved = Press(Press(listed, "j").State, "j").State;

        var result = Press(moved, "Enter");

        Assert.Equal(0, listed.SettingsScreen!.Choice);
        Assert.Equal(HintLevel.Off, result.State.Settings.StatusHints);
        Assert.Null(result.State.SettingsScreen!.Choice);
        Assert.IsType<SaveSettingsEffect>(Assert.Single(result.Effects));
    }

    [Fact]
    public void GivenTheThemeList_WhenMovingOverAWord_ThenItIsOnlyPreviewedAndNotSaved()
    {
        var listed = Press(SelectRow(Opened().State, "theme"), "Enter").State;

        var result = Press(listed, "j");

        var effect = Assert.IsType<ApplyThemeEffect>(Assert.Single(result.Effects));

        Assert.True(effect.Preview);
        Assert.Equal("dark", effect.Name);
        Assert.Equal("auto", result.State.Settings.Theme);
        Assert.Equal("dark", result.State.SettingsScreen!.Preview);
    }

    [Fact]
    public void GivenAThemePreview_WhenPressingEsc_ThenTheListClosesAndTheSavedThemeIsDrawnAgain()
    {
        var previewed = Press(Press(SelectRow(Opened().State, "theme"), "Enter").State, "j").State;

        var result = Press(previewed, "Esc");

        var effect = Assert.IsType<ApplyThemeEffect>(Assert.Single(result.Effects));

        Assert.True(effect.Preview);
        Assert.Equal("auto", effect.Name);
        Assert.NotNull(result.State.SettingsScreen);
        Assert.Null(result.State.SettingsScreen.Preview);
        Assert.Null(result.State.SettingsScreen.Choice);
    }

    [Fact]
    public void GivenAThemePreview_WhenTheScreenIsLeftByThePalette_ThenTheSavedThemeIsDrawnAgain()
    {
        var previewed = Press(Press(SelectRow(Opened().State, "theme"), "Enter").State, "j").State;

        var result = Press(previewed, ":");

        var effect = Assert.IsType<ApplyThemeEffect>(Assert.Single(result.Effects));

        Assert.True(effect.Preview);
        Assert.Equal("auto", effect.Name);
        Assert.Null(result.State.SettingsScreen);
    }

    [Fact]
    public void GivenAThemePreview_WhenClickingAnotherRow_ThenTheSavedThemeIsDrawnAgain()
    {
        var previewed = Press(Press(SelectRow(Opened().State, "theme"), "Enter").State, "j").State;

        var result = SettingsScreenReducer.Clicked(previewed, 1, ActionIds.ItemOpen);
        var restored = _reducer.Reduce(previewed, new OverlayRowClicked(MouseTarget.SettingsRow, 1, ActionIds.ItemOpen, default));

        Assert.Null(result.State.SettingsScreen!.Preview);
        Assert.Equal("auto", Assert.IsType<ApplyThemeEffect>(restored.Effects.OfType<ApplyThemeEffect>().Last()).Name);
    }

    [Fact]
    public void GivenAThemePreview_WhenPressingEnter_ThenTheThemeIsAppliedForRealAndSavedOnceDrawn()
    {
        var previewed = Press(Press(SelectRow(Opened().State, "theme"), "Enter").State, "j").State;

        var accepted = Press(previewed, "Enter");

        var effect = Assert.IsType<ApplyThemeEffect>(Assert.Single(accepted.Effects));
        var saved = _reducer.Reduce(accepted.State, new ThemeApplied("dark", true, "Theme dark."));

        Assert.False(effect.Preview);
        Assert.Equal("dark", effect.Name);
        Assert.Equal("dark", saved.State.Settings.Theme);
        Assert.Equal("dark", Assert.IsType<SaveSettingsEffect>(Assert.Single(saved.Effects)).Settings.Theme);
    }

    [Fact]
    public void GivenAPreviewAnswer_WhenTheHostDrawsIt_ThenNothingIsSavedOrChanged()
    {
        var result = _reducer.Reduce(Opened().State, new ThemeApplied("dark", true, "Theme dark.", Preview: true));

        Assert.Empty(result.Effects);
        Assert.Equal(AppSettings.Default, result.State.Settings);
    }

    [Fact]
    public void GivenAChangedSetting_WhenResetting_ThenTheDefaultIsAppliedAndSaved()
    {
        var state = SelectRow(Opened(new AppSettings(StatusHints: HintLevel.Off)).State, "hints.statusBar");

        var result = _reducer.Reduce(state, new ActionRequested(ActionIds.SettingsReset, 1, default));

        Assert.Equal(HintLevel.Full, result.State.Settings.StatusHints);
        Assert.IsType<SaveSettingsEffect>(Assert.Single(result.Effects));
    }

    [Fact]
    public void GivenADefaultSetting_WhenResetting_ThenNothingIsSavedAndItSaysSo()
    {
        var state = SelectRow(Opened().State, "hints.statusBar");

        var result = _reducer.Reduce(state, new ActionRequested(ActionIds.SettingsReset, 1, default));

        Assert.Empty(result.Effects);
        Assert.Contains("already the default", result.State.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAValueTheSettingRefuses_WhenChangingIt_ThenTheValidationErrorShowsAndNothingChanges()
    {
        var setting = SettingsRegistry.Find("hints.statusBar")!;

        var result = SettingsReducer.Change(Opened().State, setting, "maybe");

        Assert.Empty(result.Effects);
        Assert.Equal(AppSettings.Default, result.State.Settings);
        Assert.Equal("hints.statusBar takes full, minimal or off.", result.State.Message);
    }

    [Fact]
    public void GivenTheProfilesRow_WhenPressingEnter_ThenTheProfileManagerOpensInPlaceOfTheScreen()
    {
        var state = SelectRow(Opened().State, null, SettingsRowKind.Profiles);

        var result = Press(state, "Enter");

        Assert.Null(result.State.SettingsScreen);
        Assert.NotNull(result.State.ProfileList);
    }

    [Fact]
    public void GivenTheScreen_WhenPressingEsc_ThenTheScreenCloses()
    {
        var result = Press(Opened().State, "Esc");

        Assert.Null(result.State.SettingsScreen);
    }

    [Fact]
    public void GivenTheScreen_WhenPressingAKeyOfTheViewUnderneath_ThenNothingHappensToTheView()
    {
        var state = Opened().State;

        var result = Press(state, "Space i d");

        Assert.Equal(state.Session, result.State.Session);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.NotNull(result.State.SettingsScreen);
    }

    [Fact]
    public void GivenTheScreen_WhenMovingPastTheEnds_ThenTheCursorStaysOnTheFirstAndLastRow()
    {
        var rows = SettingsRows.Build(Opened().State).Count;
        var top = Press(Opened().State, "k").State.SettingsScreen!.Selected;
        var bottom = Press(Opened().State, "G").State.SettingsScreen!.Selected;
        var past = Press(Press(Opened().State, "G").State, "j").State.SettingsScreen!.Selected;

        Assert.Equal(0, top);
        Assert.Equal(rows - 1, bottom);
        Assert.Equal(rows - 1, past);
    }

    [Fact]
    public void GivenTheScreen_WhenSteppingCategories_ThenTheCursorLandsOnTheirFirstRows()
    {
        var rows = SettingsRows.Build(Opened().State);

        var next = Press(Opened().State, "l").State.SettingsScreen!.Selected;
        var again = Press(Press(Opened().State, "l").State, "h").State.SettingsScreen!.Selected;

        Assert.Equal("Hints and keys", rows[next].Category);
        Assert.Equal(0, again);
    }

    private ReduceResult Opened(AppSettings? settings = null)
    {
        var state = AppState.Initial with { Settings = settings ?? AppSettings.Default, ThemeNames = _themes };

        return _reducer.Reduce(state, new ActionRequested(ActionIds.SettingsOpen, 1, default));
    }

    private ReduceResult Press(AppState state, string keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys.Split(' '))
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key));
        }

        return result;
    }

    private static AppState SelectRow(AppState state, string? name, SettingsRowKind kind = SettingsRowKind.Setting)
    {
        var rows = SettingsRows.Build(state).ToList();
        var index = rows.FindIndex(row => row.Kind == kind && (name is null || row.Setting?.Name == name));

        return state with { SettingsScreen = new SettingsScreenState(index) };
    }
}
