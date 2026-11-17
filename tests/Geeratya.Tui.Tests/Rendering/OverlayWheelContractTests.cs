using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// One wheel scenario run against every overlay with a highlight (decisions 3 and 7 of
/// <c>docs/internal/ui-consistency.md</c>): the drawn frame gives the cells, the real resolver and reducer act. A new
/// pick list is added to <see cref="Scenario.For"/> and the two theory data lists, not given tests of its own.
/// </summary>
public class OverlayWheelContractTests
{
    private const int Width = 100;
    private const int Height = 30;
    private const string Profiles = "profiles";

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    public static TheoryData<string> BoxUsers => ["palette", "jump", "picker", "settings", Profiles];

    public static TheoryData<string> PopupUsers => ["form", "comment"];

    [Theory]
    [MemberData(nameof(BoxUsers))]
    public void GivenABoxOverlay_WhenWheelingOverARow_ThenTheHighlightMovesAndBackAndNothingIsRun(string user)
    {
        var scenario = Scenario.For(user);
        var frame = FrameBuilder.BuildStyled(scenario.State, Width, Height, _keymap)!;
        var row = RegionProbe.Rows(frame, scenario.RowTarget)[0].Region;

        var down = RegionProbe.At(scenario.State, frame, row.Left + 1, row.Top, MouseGesture.WheelDown)!;
        var up = RegionProbe.At(down.State, frame, row.Left + 1, row.Top, MouseGesture.WheelUp)!;

        Assert.Equal(scenario.Down, Scenario.Highlight(user, down.State));
        Assert.Equal(0, Scenario.Highlight(user, up.State));
        Assert.Equal(scenario.State.Mode, down.State.Mode);
        Assert.Empty(down.Effects);
    }

    [Theory]
    [MemberData(nameof(BoxUsers))]
    public void GivenABoxOverlay_WhenWheelingOverTheTopEdgeOfTheBox_ThenTheHighlightMovesToo(string user)
    {
        var scenario = Scenario.For(user);
        var frame = FrameBuilder.BuildStyled(scenario.State, Width, Height, _keymap)!;
        var box = frame.Regions.First(region => region.Target == MouseTarget.OverlayBox);

        var down = RegionProbe.At(scenario.State, frame, box.Left + (box.Width / 2), box.Top, MouseGesture.WheelDown)!;

        Assert.Equal(scenario.Down, Scenario.Highlight(user, down.State));
    }

    [Theory]
    [MemberData(nameof(BoxUsers))]
    public void GivenABoxOverlay_WhenWheelingOnTheTabRow_ThenNothingHappens(string user)
    {
        var scenario = Scenario.For(user);
        var frame = FrameBuilder.BuildStyled(scenario.State, Width, Height, _keymap)!;

        Assert.Null(RegionProbe.At(scenario.State, frame, 5, 0, MouseGesture.WheelDown));
    }

    [Theory]
    [MemberData(nameof(PopupUsers))]
    public void GivenAPopup_WhenWheelingOverItsRows_ThenTheHighlightMovesOneRowAndChoosesNothing(string user)
    {
        var scenario = Scenario.For(user);
        var frame = FrameBuilder.BuildStyled(scenario.State, Width, Height, _keymap)!;
        var row = RegionProbe.Rows(frame, scenario.RowTarget)[0].Region;

        var start = Scenario.Highlight(user, scenario.State);
        var up = RegionProbe.At(scenario.State, frame, row.Left + 1, row.Top, MouseGesture.WheelUp)!;
        var down = RegionProbe.At(up.State, frame, row.Left + 1, row.Top, MouseGesture.WheelDown)!;

        Assert.Equal(start - DefaultMouseMap.PopupWheelRows, Scenario.Highlight(user, up.State));
        Assert.Equal(start, Scenario.Highlight(user, down.State));
        Assert.Empty(down.Effects);
    }

    [Theory]
    [MemberData(nameof(PopupUsers))]
    public void GivenAPopup_WhenWheelingOverTheRestOfTheForm_ThenNothingHappens(string user)
    {
        var scenario = Scenario.For(user);
        var frame = FrameBuilder.BuildStyled(scenario.State, Width, Height, _keymap)!;
        var box = frame.Regions.First(region => region.Target == MouseTarget.OverlayBox);
        var popupRows = RegionProbe.Rows(frame, scenario.RowTarget).Select(popup => popup.Region.Top).ToHashSet();
        var other = Enumerable.Range(box.Top + 1, box.Height - 2).First(top => !popupRows.Contains(top));

        Assert.Null(RegionProbe.At(scenario.State, frame, box.Left + (box.Width / 2), other, MouseGesture.WheelDown));
        Assert.Null(RegionProbe.At(scenario.State, frame, box.Left + (box.Width / 2), box.Top, MouseGesture.WheelUp));
    }

    [Fact]
    public void GivenTheSettingsChoiceList_WhenWheeling_ThenTheHighlightMovesOneRow()
    {
        var state = SettingsScreenTests.WithThemeList();
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.SettingsChoice)[0].Region;

        var down = RegionProbe.At(state, frame, row.Left + 1, row.Top, MouseGesture.WheelDown)!;

        Assert.Equal(DefaultMouseMap.PopupWheelRows, down.State.SettingsScreen!.Choice);
    }

    private sealed record Scenario(AppState State, MouseTarget RowTarget, int Down)
    {
        public static Scenario For(string user)
        {
            return user switch
            {
                "palette" => new Scenario(
                    PaletteOverlayTests.Sample(string.Empty),
                    MouseTarget.PaletteRow,
                    DefaultMouseMap.WheelRows),
                "jump" => new Scenario(
                    JumpOverlaySnapshotTests.Sample(string.Empty),
                    MouseTarget.JumpRow,
                    DefaultMouseMap.WheelRows),
                "picker" => new Scenario(
                    SourcePickerSnapshotTests.Sample(string.Empty),
                    MouseTarget.PickerRow,
                    DefaultMouseMap.WheelRows),
                "settings" => new Scenario(
                    SettingsScreenTests.Sample(),
                    MouseTarget.SettingsRow,
                    DefaultMouseMap.WheelRows),
                Profiles => new Scenario(ProfileManagerScreenTests.Sample(), MouseTarget.ProfileRow, 1),
                "form" => new Scenario(
                    FormSuggestionOverlayTests.WithForm(FormSuggestionOverlayTests.LabelsForm(SuggestionStatus.Loaded)),
                    MouseTarget.FormChoice,
                    DefaultMouseMap.PopupWheelRows),
                _ => new Scenario(
                    CommentEditorOverlayTests.StateWith(CommentEditorOverlayTests.EditorWithPopup()),
                    MouseTarget.EditorChoice,
                    DefaultMouseMap.PopupWheelRows),
            };
        }

        public static int Highlight(string user, AppState state)
        {
            return user switch
            {
                "palette" => state.PaletteSelected,
                "jump" => state.Jump!.Selected,
                "picker" => state.Picker!.Selected,
                "settings" => state.SettingsScreen!.Selected,
                Profiles => state.ProfileList!.Selected,
                "form" => FormReducer.OpenForm(state)!.Suggest!.Cursor,
                _ => state.Editor!.Suggest!.Cursor,
            };
        }
    }
}
