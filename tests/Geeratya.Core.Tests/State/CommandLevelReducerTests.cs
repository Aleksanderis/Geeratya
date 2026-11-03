using Geeratya.Core.Keymap;
using Geeratya.Core.Settings;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public class CommandLevelReducerTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.UnixEpoch + TimeSpan.FromHours(1);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenEveryday_WhenPressingSpaceStar_ThenTheLevelIsAdvancedWithAMessageAndNothingIsSaved()
    {
        var result = Reduce(Press(AppState.Initial, "Space"), "*");

        Assert.Equal(CommandLevel.Advanced, result.State.Settings.CommandLevel);
        Assert.Equal("Command level: advanced", result.State.Status?.Line);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAdvanced_WhenPressingSpaceStar_ThenTheLevelReturnsToEveryday()
    {
        var advanced = AppState.Initial with
        {
            Settings = AppSettings.Default with { CommandLevel = CommandLevel.Advanced },
        };

        var result = Press(Press(advanced, "Space"), "*");

        Assert.Equal(CommandLevel.Everyday, result.Settings.CommandLevel);
        Assert.Equal("Command level: everyday", result.Status?.Line);
    }

    [Fact]
    public void GivenHelpScrolledFarInAdvanced_WhenSwitchingToEveryday_ThenTheScrollStaysInsideTheShorterHelp()
    {
        var keymap = DefaultKeymap.Load();
        var everydayLines = KeymapDocument.RenderHelpLines(keymap).Count;
        var advancedLines = KeymapDocument.RenderHelpLines(keymap, CommandLevel.Advanced).Count;
        var help = AppState.Initial with
        {
            Settings = AppSettings.Default with { CommandLevel = CommandLevel.Advanced },
            Mode = InputMode.Help,
            HelpScroll = advancedLines - 1,
        };

        var result = Press(Press(help, "Space"), "*");

        Assert.True(advancedLines > everydayLines);
        Assert.Equal(InputMode.Help, result.Mode);
        Assert.True(result.HelpScroll <= everydayLines - 1);
    }

    [Fact]
    public void GivenAnAdvancedAction_WhenTheLevelIsEveryday_ThenItsKeyStillRuns()
    {
        var pending = Press(Press(AppState.Initial, "Space"), "k");

        var result = Press(pending, "t");

        Assert.Equal(CommandLevel.Everyday, result.Settings.CommandLevel);
        Assert.Equal(string.Empty, result.PendingKeys);
        Assert.Equal(KeyResolutionKind.Action, DefaultKeymap.Load().Resolve(InputMode.Normal, "Space k", "t").Kind);
    }

    [Fact]
    public void GivenSetCommandLevel_WhenSubmittingThePalette_ThenTheLevelChangesAndIsSaved()
    {
        var palette = AppState.Initial with { Mode = InputMode.Command, PaletteText = "set commandlevel advanced" };

        var result = Reduce(palette, "Enter");

        Assert.Equal(CommandLevel.Advanced, result.State.Settings.CommandLevel);
        Assert.IsType<SaveSettingsEffect>(Assert.Single(result.Effects));
    }

    private AppState Press(AppState state, string key)
    {
        return Reduce(state, key).State;
    }

    private ReduceResult Reduce(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key, _now));
    }
}
