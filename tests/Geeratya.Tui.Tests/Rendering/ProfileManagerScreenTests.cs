using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ProfileManagerScreenTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _dev = new("dev", "https://dev.atlassian.net", "me@dev.test");

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenTheOpenList_WhenReadingTheRegions_ThenEachProfileHasARowWithItsName(int width, int height)
    {
        var frame = FrameBuilder.BuildStyled(Sample(), width, height, _keymap)!;
        var rows = RegionProbe.Rows(frame, MouseTarget.ProfileRow);

        Assert.Equal([0, 1], rows.Select(row => row.Index));
        Assert.Contains("work", rows[0].Text, StringComparison.Ordinal);
        Assert.Contains("dev", rows[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenList_WhenBuildingTheFrame_ThenTheActiveProfileAndTheKeysAreShown()
    {
        var text = string.Join('\n', FrameBuilder.BuildStyled(Sample(), 100, 24, _keymap)!.PlainLines());

        Assert.Contains("●", text, StringComparison.Ordinal);
        Assert.Contains("Ctrl-x remove", text, StringComparison.Ordinal);
        Assert.Contains("Enter switch", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenManager_WhenBuildingTheFrame_ThenItIsTheWholeBodyWithTheProfilesChipAndNoTabs()
    {
        var frame = FrameBuilder.BuildStyled(Sample(), 100, 24, _keymap)!;
        var lines = frame.PlainLines();

        Assert.Contains("Profiles", frame.Header[0].PlainText, StringComparison.Ordinal);
        Assert.StartsWith("╭", frame.Body[0].PlainText, StringComparison.Ordinal);
        Assert.Contains("Profiles 2", lines[2], StringComparison.Ordinal);
        Assert.Contains("Details", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAKnownTokenSource_WhenBuildingTheFrame_ThenTheDetailsNameTheSourceAndNeverTheToken()
    {
        var state = Sample() with
        {
            TokenSources = new Dictionary<string, TokenSourceKind> { [_work.CredentialKey] = TokenSourceKind.SecretStore },
        };
        var text = string.Join('\n', FrameBuilder.BuildStyled(state, 100, 24, _keymap)!.PlainLines());
        var other = Sample() with { ProfileList = new ProfileListState(1) };
        var otherText = string.Join('\n', FrameBuilder.BuildStyled(other, 100, 24, _keymap)!.PlainLines());

        Assert.Contains("Token    OS secret store", text, StringComparison.Ordinal);
        Assert.Contains("https://work.atlassian.net", text, StringComparison.Ordinal);
        Assert.Contains("looking up", otherText, StringComparison.Ordinal);
        Assert.Contains("not checked", otherText, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnEnvironmentTokenAndNone_WhenReadingTheTokenText_ThenTheSourceIsNamed()
    {
        var sources = new Dictionary<string, TokenSourceKind>
        {
            [_work.CredentialKey] = TokenSourceKind.EnvironmentVariable,
            [_dev.CredentialKey] = TokenSourceKind.None,
        };
        var state = Sample() with { TokenSources = sources };

        Assert.Equal("env GEERATYA_TOKEN", ProfileManagerScreen.TokenText(state, _work));
        Assert.Equal("not set", ProfileManagerScreen.TokenText(state, _dev));
    }

    [Fact]
    public void GivenNoProfile_WhenBuildingTheFrame_ThenTheFirstRunScreenOffersToAddOne()
    {
        var state = ProfileReducer.FirstRun(AppState.Initial);
        var text = string.Join('\n', FrameBuilder.BuildStyled(state, 100, 24, _keymap)!.PlainLines());

        Assert.NotNull(state.ProfileList);
        Assert.Equal(InputMode.Insert, state.Mode);
        Assert.Contains("No profile yet.", text, StringComparison.Ordinal);
        Assert.Contains("Ctrl-a adds one.", text, StringComparison.Ordinal);
        Assert.Contains("API token", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenANarrowTerminal_WhenBuildingTheFrame_ThenTheListShowsWithoutTheDetailsPane()
    {
        var text = string.Join('\n', FrameBuilder.BuildStyled(Sample(), 50, 14, _keymap)!.PlainLines());

        Assert.DoesNotContain("Details", text, StringComparison.Ordinal);
        Assert.Contains("work", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenList_WhenClickingTheSecondProfile_ThenTheSwitchStartsLikeTheCommand()
    {
        var state = Sample();
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
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
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.ProfileRow).Single(candidate => candidate.Index == 1);

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.RightClick)!;

        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Equal("dev", result.State.Confirm!.Target);
        Assert.Contains("cannot be undone", result.State.Confirm.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenList_WhenClickingTheAddAndEditButtons_ThenTheFormsOpenAndTheKeysAreShown()
    {
        var state = Sample();
        var frame = FrameBuilder.BuildStyled(state, 100, 24, _keymap)!;
        var add = Assert.Single(RegionProbe.Rows(frame, MouseTarget.ProfileAddButton)).Region;
        var edit = Assert.Single(RegionProbe.Rows(frame, MouseTarget.ProfileEditButton)).Region;
        var text = string.Join('\n', frame.PlainLines());

        var added = RegionProbe.Click(state, frame, add, MouseGesture.Click)!;
        var edited = RegionProbe.Click(state, frame, edit, MouseGesture.Click)!;

        Assert.False(added.State.Form!.IsEdit);
        Assert.Equal("work", edited.State.Form!.EditingName);
        Assert.Contains("Ctrl-a add", text, StringComparison.Ordinal);
        Assert.Contains("Ctrl-e edit", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnyTerminalSize_WhenBuildingTheFrame_ThenItNeverThrows()
    {
        for (var width = 0; width <= 30; width++)
        {
            for (var height = 0; height <= 12; height++)
            {
                FrameBuilder.BuildStyled(Sample(), width, height, _keymap);
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
