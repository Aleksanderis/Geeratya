using Geeratya.Core.Lists;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
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

    [Theory]
    [InlineData("Down")]
    [InlineData("Ctrl-n")]
    public void GivenAnyPickList_WhenPressingTheNextKeyAndThenThePreviousKey_ThenTheHighlightMovesOneRowAndBack(string key)
    {
        var previous = key == "Down" ? "Up" : "Ctrl-p";
        var reducer = new AppReducer(_keymap);

        foreach (var user in new[] { "palette", "jump", "picker", Profiles, "form", "comment" })
        {
            var scenario = Scenario.For(user);
            var start = Scenario.Highlight(user, scenario.State);
            var after = reducer.Reduce(scenario.State, new KeyPressed(user == "form" || user == "comment" ? "Up" : key));
            var moved = Scenario.Highlight(user, after.State);

            Assert.True(moved != start, $"{user}: {key} did not move the highlight");

            var back = reducer.Reduce(after.State, new KeyPressed(user == "form" || user == "comment" ? "Down" : previous));

            Assert.Equal(start, Scenario.Highlight(user, back.State));
        }
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

    [Theory]
    [MemberData(nameof(BoxUsers))]
    [MemberData(nameof(PopupUsers))]
    public void GivenAPickList_WhenClickingTheSecondRow_ThenThatRowIsChosenAndNotTheHighlightedOne(string user)
    {
        var scenario = Scenario.For(user);
        var frame = FrameBuilder.BuildStyled(scenario.State, Width, Height, _keymap)!;
        var rows = RegionProbe.Rows(frame, scenario.RowTarget);
        var second = rows.First(row => row.Index == 1);

        var clicked = RegionProbe.Click(scenario.State, frame, second.Region, MouseGesture.Click);

        Assert.NotNull(clicked);
        Assert.True(Scenario.Chose(user, scenario.State, clicked.State), $"{user}: the click did not choose row 1");
    }

    [Theory]
    [MemberData(nameof(BoxUsers))]
    [MemberData(nameof(PopupUsers))]
    public void GivenAPickList_WhenDrawn_ThenEveryRowRegionSitsOnItsDrawnRow(string user)
    {
        var scenario = Scenario.For(user);
        var frame = FrameBuilder.BuildStyled(scenario.State, Width, Height, _keymap)!;

        var rows = RegionProbe.Rows(frame, scenario.RowTarget);

        Assert.True(rows.Count >= 2, $"{user}: fewer than two clickable rows");
        Assert.Equal(rows.Count, rows.Select(row => row.Region.Top).Distinct().Count());
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Text.Trim('│', ' ', '▲', '▼'))));
    }

    [Fact]
    public void GivenTheContextMenu_WhenClickingTheSecondItem_ThenItRuns()
    {
        var state = ContextMenuOverlayTests.Sample();
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var second = RegionProbe.Rows(frame, MouseTarget.ContextMenu).First(row => row.Index == 1);

        var clicked = RegionProbe.Click(state, frame, second.Region, MouseGesture.Click);

        Assert.NotNull(clicked);
        Assert.Null(clicked.State.ContextMenu);
    }

    [Fact]
    public void GivenTheSettingsChoiceList_WhenClickingTheSecondWord_ThenItIsTaken()
    {
        var state = SettingsScreenTests.WithThemeList();
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var second = RegionProbe.Rows(frame, MouseTarget.SettingsChoice).First(row => row.Index == 1);

        var clicked = RegionProbe.Click(state, frame, second.Region, MouseGesture.Click);

        Assert.NotNull(clicked);
        Assert.Null(clicked.State.SettingsScreen!.Choice);
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
                    FormSuggestionOverlayTests.WithForm(FormSuggestionOverlayTests.LabelsForm(PickStatus.Ready)),
                    MouseTarget.FormChoice,
                    DefaultMouseMap.PopupWheelRows),
                _ => new Scenario(
                    CommentEditorOverlayTests.StateWith(CommentEditorOverlayTests.EditorWithPopup()),
                    MouseTarget.EditorChoice,
                    DefaultMouseMap.PopupWheelRows),
            };
        }

        public static bool Chose(string user, AppState before, AppState after)
        {
            return user switch
            {
                "palette" => after.Mode != InputMode.Command,
                "jump" => after.Jump is null,
                "picker" => after.Picker is null,
                "settings" => after.SettingsScreen!.Selected == 1,
                Profiles => after.ProfileList is null,
                "form" => Chosen(after) == Chosen(before) + 1,
                _ => after.Editor!.Suggest is null,
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

        private static int Chosen(AppState state)
        {
            var form = FormReducer.OpenForm(state)!;

            return form.Fields[form.Focus].Value.Chosen.Count;
        }
    }
}
