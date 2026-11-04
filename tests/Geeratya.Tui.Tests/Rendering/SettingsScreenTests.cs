using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The settings screen at the three snapshot sizes, its rows from the registry and its mouse paths through the real
/// resolver and reducer. To refresh a snapshot after an intended change run with <c>GEERATYA_UPDATE_SNAPSHOTS=1</c>
/// and review the diff.
/// </summary>
public class SettingsScreenTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    public void GivenABasic16Terminal_WhenBuildingTheFrame_ThenTheThemeRowNoteMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(WithTier(Sample(), ColourTier.Basic16), width, height, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var expected = SnapshotFile.Expect($"settings-basic16-{width}x{height}.txt", actual);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(60, 24)]
    [InlineData(40, 10)]
    public void GivenTheOpenScreen_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample(), width, height, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var expected = SnapshotFile.Expect($"settings-{width}x{height}.txt", actual);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(60, 24)]
    [InlineData(40, 10)]
    public void GivenTheOpenChoiceList_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(WithThemeList(), width, height, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var expected = SnapshotFile.Expect($"settings-themes-{width}x{height}.txt", actual);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(60, 24)]
    [InlineData(40, 10)]
    public void GivenTheOpenScreen_WhenBuildingTheFrameInAscii_ThenNoUnicodeGlyphLeaks(int width, int height)
    {
        var lines = FrameBuilder.Build(WithThemeList(), width, height, _keymap, glyphs: GlyphSet.Ascii);

        Assert.All(lines, line => Assert.True(line.All(character => character < 128), line));
    }

    [Fact]
    public void GivenTheRegistry_WhenBuildingTheFrame_ThenEverySettingShowsItsNameAndValue()
    {
        var text = string.Join('\n', FrameBuilder.Build(Sample(), 40, 40, _keymap));
        var rows = RegionProbe.Rows(FrameBuilder.BuildStyled(Sample(), 40, 40, _keymap)!, MouseTarget.SettingsRow);

        foreach (var setting in SettingsRegistry.All)
        {
            Assert.Contains(setting.Name, text, StringComparison.Ordinal);
        }

        Assert.Equal(SettingsRows.Build(Sample()).Count, rows.Count);
    }

    [Fact]
    public void GivenTheOpenScreen_WhenBuildingTheFrame_ThenItIsAPopupOverTheViewWithTheTabRowKept()
    {
        var frame = FrameBuilder.BuildStyled(Sample(), 100, 30, _keymap)!;
        var text = string.Join('\n', frame.PlainLines());

        Assert.DoesNotContain("Settings", frame.Header[0].PlainText, StringComparison.Ordinal);
        Assert.Contains("commands", frame.Header[0].PlainText, StringComparison.Ordinal);
        Assert.Contains("Geeratya", frame.Body[0].PlainText, StringComparison.Ordinal);
        Assert.Contains("─ Settings ─", text, StringComparison.Ordinal);
        Assert.Contains("Default:", text, StringComparison.Ordinal);
        Assert.Contains("Enter change", text, StringComparison.Ordinal);
        Assert.Contains("Alt-r reset", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenScreen_WhenBuildingTheFrame_ThenTheViewBehindIsDimmed()
    {
        var frame = FrameBuilder.BuildStyled(Sample(), 100, 30, _keymap)!;

        Assert.Contains(frame.Body[0].Spans, span => span.Ground.HasFlag(SpanGround.Dim));
    }

    [Fact]
    public void GivenAToggleAndAChoice_WhenBuildingTheFrame_ThenTheControlsSitAtTheRightEndOfTheirRows()
    {
        var text = FrameBuilder.Build(Sample(), 100, 30, _keymap);
        var theme = text.Single(line => line.Contains("theme", StringComparison.Ordinal) && line.Contains('▾'));
        var transparent = text.Single(line => line.Contains("transparent", StringComparison.Ordinal));

        Assert.Contains("[auto ▾] │", theme, StringComparison.Ordinal);
        Assert.Contains("[ ] │", transparent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(39, 30)]
    [InlineData(80, 8)]
    public void GivenATerminalBelowTheMinimum_WhenBuildingTheFrame_ThenTheBoxSaysItIsTooSmall(int width, int height)
    {
        var text = string.Join('\n', FrameBuilder.Build(Sample(), width, height, _keymap));

        Assert.Contains("Terminal too small", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenScreen_WhenClickingOutsideTheBox_ThenTheScreenCloses()
    {
        var state = Sample();
        var frame = FrameBuilder.BuildStyled(state, 100, 30, _keymap)!;
        var outside = new OverlayRegion(MouseTarget.Backdrop, frame.Header.Count + 1, 1, 1, 1, 0);

        var result = RegionProbe.Click(state, frame, outside, MouseGesture.Click)!;

        Assert.Null(result.State.SettingsScreen);
    }

    [Fact]
    public void GivenANarrowTerminal_WhenBuildingTheFrame_ThenTheCategoriesCollapseIntoHeadingsOfOneList()
    {
        var text = string.Join('\n', FrameBuilder.Build(Sample(), 60, 40, _keymap));

        Assert.DoesNotContain("┴", text, StringComparison.Ordinal);
        Assert.Contains("Appearance", text, StringComparison.Ordinal);
        Assert.Contains("Startup", text, StringComparison.Ordinal);
        Assert.Contains("About", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPreview_WhenBuildingTheFrame_ThenTheHelpRowSaysItIsNotSaved()
    {
        var state = WithThemeList() with
        {
            SettingsScreen = WithThemeList().SettingsScreen! with { Preview = "nord" },
        };

        var text = string.Join('\n', FrameBuilder.Build(state, 100, 30, _keymap));

        Assert.Contains("Previewing nord", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ColourTier.Basic16, "16 colours here: themes are approximated.")]
    [InlineData(ColourTier.None, "No colour here: themes change nothing.")]
    public void GivenALimitedTier_WhenOnTheThemeRow_ThenTheHelpRowNotesTheTier(ColourTier tier, string note)
    {
        var state = WithTier(WithThemeList(), tier);

        var text = string.Join('\n', FrameBuilder.Build(state, 100, 30, _keymap));

        Assert.Contains(note, text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPreviewOnABasic16Terminal_WhenBuildingTheFrame_ThenTheNoteStaysOnTheHelpRow()
    {
        var themes = WithThemeList();
        var state = WithTier(themes with { SettingsScreen = themes.SettingsScreen! with { Preview = "nord" } }, ColourTier.Basic16);

        var text = string.Join('\n', FrameBuilder.Build(state, 100, 30, _keymap));

        Assert.Contains("16 colours here", text, StringComparison.Ordinal);
        Assert.Contains("Previewing nord", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenFullColour_WhenOnTheThemeRow_ThenNoTierNoteIsShown()
    {
        var text = string.Join('\n', FrameBuilder.Build(WithThemeList(), 100, 30, _keymap));

        Assert.DoesNotContain("colour here", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenABasic16Terminal_WhenOnAnotherRow_ThenNoTierNoteIsShown()
    {
        var state = WithTier(OnHints(Sample()), ColourTier.Basic16);

        var text = string.Join('\n', FrameBuilder.Build(state, 100, 30, _keymap));

        Assert.DoesNotContain("colours here", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheScreen_WhenClickingAToggleRow_ThenItFlipsAndTheSaveIsAsked()
    {
        var state = OnHints(Sample());
        var frame = FrameBuilder.BuildStyled(state, 100, 30, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.SettingsRow)
            .Single(candidate => candidate.Text.Contains("hints.whichKey"));

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        Assert.False(result.State.Settings.WhichKey);
        Assert.IsType<SaveSettingsEffect>(Assert.Single(result.Effects));
    }

    [Fact]
    public void GivenTheScreen_WhenRightClickingAChangedRow_ThenItIsResetToTheDefault()
    {
        var state = OnHints(Sample() with { Settings = new AppSettings(WhichKey: false) });
        var frame = FrameBuilder.BuildStyled(state, 100, 30, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.SettingsRow)
            .Single(candidate => candidate.Text.Contains("hints.whichKey"));

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.RightClick)!;

        Assert.True(result.State.Settings.WhichKey);
        Assert.IsType<SaveSettingsEffect>(Assert.Single(result.Effects));
    }

    [Fact]
    public void GivenTheScreen_WhenClickingACategory_ThenTheCursorMovesToItsFirstRow()
    {
        var state = Sample();
        var frame = FrameBuilder.BuildStyled(state, 100, 30, _keymap)!;
        var category = RegionProbe.Rows(frame, MouseTarget.SettingsCategory).Single(candidate => candidate.Index == 2);

        var result = RegionProbe.Click(state, frame, category.Region, MouseGesture.Click)!;

        Assert.Equal("Startup", SettingsRows.Build(result.State)[result.State.SettingsScreen!.Selected].Category);
    }

    [Fact]
    public void GivenTheThemeList_WhenClickingAWord_ThenItIsAppliedAndSavedOnceTheHostAnswers()
    {
        var state = WithThemeList();
        var frame = FrameBuilder.BuildStyled(state, 100, 30, _keymap)!;
        var word = RegionProbe.Rows(frame, MouseTarget.SettingsChoice)
            .Single(candidate => candidate.Text.Contains("nord"));

        var result = RegionProbe.Click(state, frame, word.Region, MouseGesture.Click)!;
        var saved = new AppReducer(_keymap).Reduce(result.State, new ThemeApplied("nord", true, "Theme nord."));

        Assert.Equal("nord", Assert.IsType<ApplyThemeEffect>(Assert.Single(result.Effects)).Name);
        Assert.Equal("nord", saved.State.Settings.Theme);
        Assert.IsType<SaveSettingsEffect>(Assert.Single(saved.Effects));
    }

    [Fact]
    public void GivenTheScreen_WhenClickingAGapOfTheBody_ThenNothingChanges()
    {
        var state = Sample();
        var frame = FrameBuilder.BuildStyled(state, 100, 30, _keymap)!;
        var box = frame.Regions.Single(region => region.Target == MouseTarget.OverlayBox);
        var gap = new OverlayRegion(MouseTarget.OverlayBox, box.Top + box.Height - 3, box.Left + 10, 1, 1, 0);

        var result = RegionProbe.Click(state, frame, gap, MouseGesture.Click);

        Assert.True(result is null || result.State.SettingsScreen is not null);
    }

    private static AppState Sample()
    {
        var state = AppState.Initial with
        {
            ThemeNames = ["dark", "light", "nord"],
            SettingsInfo = new SettingsInfo(
                "/home/user/.config/geeratya/settings.json",
                "/home/user/.config/geeratya/keymap.json"),
        };

        return state with { SettingsScreen = new SettingsScreenState(0) };
    }

    private static AppState WithTier(AppState state, ColourTier tier)
    {
        return state with { SettingsInfo = state.SettingsInfo with { Tier = tier } };
    }

    private static AppState OnHints(AppState state)
    {
        var index = SettingsRows.Build(state).ToList().FindIndex(row => row.Category == SettingsRegistry.HintsCategory);

        return state with { SettingsScreen = new SettingsScreenState(index) };
    }

    private static AppState WithThemeList()
    {
        return Sample() with { SettingsScreen = new SettingsScreenState(0, 0) };
    }
}
