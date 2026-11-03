using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ProfileListOverlayTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _dev = new("dev", "https://dev.atlassian.net", "me@dev.test");

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenTheOpenList_WhenReadingTheRegions_ThenEachProfileHasARowWithItsName(int width, int height)
    {
        var frame = FrameBuilder.BuildStyled(Sample(), width, height, _helpLines, _keymap)!;
        var rows = RegionProbe.Rows(frame, MouseTarget.ProfileRow);

        Assert.Equal([0, 1], rows.Select(row => row.Index));
        Assert.Contains("work", rows[0].Text, StringComparison.Ordinal);
        Assert.Contains("dev", rows[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenList_WhenBuildingTheFrame_ThenTheActiveProfileAndTheKeysAreShown()
    {
        var text = string.Join('\n', FrameBuilder.BuildStyled(Sample(), 100, 24, _helpLines, _keymap)!.PlainLines());

        Assert.Contains("(active)", text, StringComparison.Ordinal);
        Assert.Contains("Ctrl-x remove", text, StringComparison.Ordinal);
        Assert.Contains("Enter switch", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenList_WhenClickingTheSecondProfile_ThenTheSwitchStartsLikeTheCommand()
    {
        var state = Sample();
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _helpLines, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.ProfileRow).Single(candidate => candidate.Index == 1);

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        var effect = Assert.IsType<SaveProfilesEffect>(Assert.Single(result.Effects));

        Assert.Equal("dev", effect.Catalog.ActiveName);
        Assert.Null(result.State.ProfileList);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenTheOpenList_WhenRightClickingAProfile_ThenTheRemovalIsConfirmedAndNothingRuns()
    {
        var state = Sample();
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _helpLines, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.ProfileRow).Single(candidate => candidate.Index == 1);

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.RightClick)!;

        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Equal("dev", result.State.Confirm!.Target);
        Assert.Contains("cannot be undone", result.State.Confirm.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnyTerminalSize_WhenBuildingTheFrame_ThenItNeverThrows()
    {
        for (var width = 0; width <= 30; width++)
        {
            for (var height = 0; height <= 12; height++)
            {
                FrameBuilder.BuildStyled(Sample(), width, height, _helpLines, _keymap);
            }
        }
    }

    internal static AppState Sample()
    {
        var catalog = ProfileCatalog.Empty.With(_work).With(_dev).Activate("work");

        return AppState.Initial with
        {
            Mode = InputMode.Insert,
            Profiles = catalog,
            Session = SiteSession.Empty(_work),
            ProfileList = new ProfileListState(0),
        };
    }
}
