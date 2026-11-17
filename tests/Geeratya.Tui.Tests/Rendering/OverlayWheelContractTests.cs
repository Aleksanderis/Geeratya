using System.Globalization;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Text;
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
    public void GivenAPickList_WhenClickingTheSecondRow_ThenTheResultIsThatOfHighlightingItAndPressingEnter(string user)
    {
        var scenario = Scenario.For(user);
        var frame = FrameBuilder.BuildStyled(scenario.State, Width, Height, _keymap)!;
        var second = RegionProbe.Rows(frame, scenario.RowTarget).First(row => row.Index == 1);

        var clicked = RegionProbe.Click(scenario.State, frame, second.Region, MouseGesture.Click);
        var keyed = new AppReducer(_keymap).Reduce(Scenario.Highlighted(user, scenario.State, 1), new KeyPressed("Enter"));

        Assert.NotNull(clicked);
        Assert.Equal(Scenario.Outcome(user, keyed), Scenario.Outcome(user, clicked));
        Assert.NotEqual(
            Scenario.Outcome(user, new AppReducer(_keymap).Reduce(Scenario.Highlighted(user, scenario.State, 0), new KeyPressed("Enter"))),
            Scenario.Outcome(user, clicked));
    }

    [Theory]
    [MemberData(nameof(BoxUsers))]
    [MemberData(nameof(PopupUsers))]
    public void GivenAPickList_WhenHighlightingEachOfItsFirstRows_ThenTheHighlightIsDrawnOnTheRowOfThatRegion(string user)
    {
        var scenario = Scenario.For(user);
        var rows = RegionProbe.Rows(FrameBuilder.BuildStyled(scenario.State, Width, Height, _keymap)!, scenario.RowTarget);

        Assert.True(rows.Count >= 2, $"{user}: fewer than two clickable rows");

        foreach (var row in rows.Take(3))
        {
            var state = Scenario.Highlighted(user, scenario.State, row.Index);
            var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
            var region = RegionProbe.Rows(frame, scenario.RowTarget).First(candidate => candidate.Index == row.Index).Region;
            var line = frame.Header.Concat(frame.Body).ElementAt(region.Top);

            Assert.True(
                line.Spans.Any(span => span.Role == TextRole.Selected),
                $"{user}: row {row.Index} is not the highlighted line at its region");
        }
    }

    [Fact]
    public void GivenTheContextMenu_WhenClickingTheSecondItem_ThenItRunsLikeHighlightingItAndPressingEnter()
    {
        var state = ContextMenuOverlayTests.Sample();
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var second = RegionProbe.Rows(frame, MouseTarget.ContextMenu).First(row => row.Index == 1);
        var reducer = new AppReducer(_keymap);

        var clicked = RegionProbe.Click(state, frame, second.Region, MouseGesture.Click)!;
        var keyed = reducer.Reduce(state with { ContextMenu = state.ContextMenu! with { Selected = 1 } }, new KeyPressed("Enter"));
        var first = reducer.Reduce(state with { ContextMenu = state.ContextMenu! with { Selected = 0 } }, new KeyPressed("Enter"));

        Assert.Equal(Scenario.Outcome("menu", keyed), Scenario.Outcome("menu", clicked));
        Assert.NotEqual(Scenario.Outcome("menu", first), Scenario.Outcome("menu", clicked));
    }

    [Fact]
    public void GivenTheSettingsChoiceList_WhenClickingTheSecondWord_ThenItIsTakenLikeEnterOnIt()
    {
        var state = SettingsScreenTests.WithThemeList();
        var frame = FrameBuilder.BuildStyled(state, Width, Height, _keymap)!;
        var second = RegionProbe.Rows(frame, MouseTarget.SettingsChoice).First(row => row.Index == 1);
        var reducer = new AppReducer(_keymap);

        var clicked = RegionProbe.Click(state, frame, second.Region, MouseGesture.Click)!;
        var keyed = reducer.Reduce(
            state with { SettingsScreen = state.SettingsScreen! with { Choice = 1 } },
            new KeyPressed("Enter"));
        var first = reducer.Reduce(
            state with { SettingsScreen = state.SettingsScreen! with { Choice = 0 } },
            new KeyPressed("Enter"));

        Assert.Equal(Scenario.Outcome("choice", keyed), Scenario.Outcome("choice", clicked));
        Assert.NotEqual(Scenario.Outcome("choice", first), Scenario.Outcome("choice", clicked));
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

        public static AppState Highlighted(string user, AppState state, int row)
        {
            return user switch
            {
                "palette" => state with { PaletteSelected = row },
                "jump" => state with { Jump = state.Jump! with { Selected = row } },
                "picker" => state with { Picker = state.Picker! with { Selected = row } },
                "settings" => state with { SettingsScreen = new SettingsScreenState(row) },
                Profiles => state with { ProfileList = state.ProfileList! with { Selected = row } },
                "form" => FormSuggestionOverlayTests.WithForm(
                    FormReducer.OpenForm(state)! with { Suggest = FormReducer.OpenForm(state)!.Suggest! with { Cursor = row } }),
                _ => state with { Editor = state.Editor! with { Suggest = state.Editor.Suggest! with { Cursor = row } } },
            };
        }

        // What a choice leaves behind: the mode, the message, the effects, which overlays stay open, the views, the
        // chosen values and the editor text, as text so that two outcomes compare by value.
        public static string Outcome(string user, ReduceResult result)
        {
            var state = result.State;
            var form = FormReducer.OpenForm(state);
            var parts = new List<string>
            {
                state.Mode.ToString(),
                state.Message ?? string.Empty,
                string.Join(",", result.Effects.Select(effect => effect.ToString())),
                $"{state.Jump is null}{state.Picker is null}{state.ProfileList is null}{state.ContextMenu is null}",
                string.Join(",", state.Session.Tabs.Select(tab => tab.Definition.ToString())),
                string.Join(",", form?.Fields.SelectMany(field => field.Value.Chosen) ?? []),
                state.Editor?.Buffer.Text ?? string.Empty,
                state.SettingsScreen?.Selected.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                state.Confirm?.ToString() ?? string.Empty,
            };

            return $"{user}: {string.Join("|", parts)}";
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
