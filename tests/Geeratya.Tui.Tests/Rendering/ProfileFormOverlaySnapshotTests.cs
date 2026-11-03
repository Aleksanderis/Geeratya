using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The add-profile prompt box at the three snapshot sizes, the status-line fallback and the masked token. To refresh
/// after an intended change run with <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> and review the diff.
/// </summary>
public class ProfileFormOverlaySnapshotTests
{
    private const string Token = "s3cr3t-token-value";

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenTheTokenStep_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample(ProfileFormField.Token, null), width, height, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var expected = SnapshotFile.Expect($"profile-form-{width}x{height}.txt", actual);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenTheTokenStep_WhenBuildingTheFrame_ThenTheTokenNeverAppearsInClearText(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample(ProfileFormField.Token, null), width, height, _keymap);

        Assert.DoesNotContain(lines, line => line.Contains("s3cr3t", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("***", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAnError_WhenBuildingTheFrame_ThenTheBoxShowsIt()
    {
        var lines = FrameBuilder.Build(Sample(ProfileFormField.Name, "The name cannot be empty."), 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("! The name cannot be empty.", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAnOpenBox_WhenBuildingTheFrame_ThenTheStatusLineKeepsOnlyTheStep()
    {
        var lines = FrameBuilder.Build(Sample(ProfileFormField.Email, null), 80, 24, _keymap);
        var status = lines[^1];

        Assert.Contains("INSERT Account email", status, StringComparison.Ordinal);
        Assert.DoesNotContain("_", status, StringComparison.Ordinal);
        Assert.Contains(lines, line => line.Contains("> Account", StringComparison.Ordinal) && line.Contains('│'));
    }

    [Theory]
    [InlineData(80, 6)]
    public void GivenATooSmallScreen_WhenBuildingTheFrame_ThenThePromptStaysInTheStatusLine(int width, int height)
    {
        var lines = FrameBuilder.Build(Sample(ProfileFormField.Token, null), width, height, _keymap);

        Assert.DoesNotContain(lines.Take(lines.Count - 1), line => line.Contains('┌'));
        Assert.Contains("[4/4]", lines[^1], StringComparison.Ordinal);
        Assert.Contains("***_", lines[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Contains("s3cr3t", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenATooNarrowScreen_WhenBuildingTheFrame_ThenNothingIsBoxedAndTheTokenStaysHidden()
    {
        var lines = FrameBuilder.Build(Sample(ProfileFormField.Token, null), 20, 24, _keymap);

        Assert.DoesNotContain(lines, line => line.Contains('┌'));
        Assert.DoesNotContain(lines, line => line.Contains("s3cr3t", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ProfileFormField.Name, "Enter next")]
    [InlineData(ProfileFormField.SiteUrl, "Enter next")]
    [InlineData(ProfileFormField.Email, "Enter next")]
    [InlineData(ProfileFormField.Token, "Enter save")]
    public void GivenEachStep_WhenBuildingTheFrame_ThenTheStatusHintSaysWhatEnterDoes(ProfileFormField field, string expected)
    {
        var lines = FrameBuilder.Build(Sample(field, null), 100, 30, _keymap);

        Assert.Contains(expected, lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheEditForm_WhenBuildingTheFrame_ThenItNamesTheProfileAndSaysTheTokenIsKept()
    {
        var lines = FrameBuilder.Build(Editing(), 80, 24, _keymap);
        var text = string.Join('\n', lines);

        Assert.Contains("Edit profile · work", text, StringComparison.Ordinal);
        Assert.Contains("kept (type to replace)", text, StringComparison.Ordinal);
        Assert.Contains("https://work.atlassian.net", text, StringComparison.Ordinal);
        Assert.Contains("Ctrl-s save", text, StringComparison.Ordinal);
        Assert.Contains("Alt-t test", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheForm_WhenClickingFieldsAndButtons_ThenTheyFocusTestSaveAndCancelLikeTheKeys()
    {
        var state = Editing();
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var fields = RegionProbe.Rows(frame, MouseTarget.FormField);
        var test = Assert.Single(RegionProbe.Rows(frame, MouseTarget.ProfileTestButton)).Region;
        var save = Assert.Single(RegionProbe.Rows(frame, MouseTarget.ProfileSaveButton)).Region;
        var cancel = Assert.Single(RegionProbe.Rows(frame, MouseTarget.ProfileCancelButton)).Region;

        var focused = RegionProbe.Click(state, frame, fields[2].Region, MouseGesture.Click)!;
        var tested = RegionProbe.Click(state, frame, test, MouseGesture.Click)!;
        var saved = RegionProbe.Click(state, frame, save, MouseGesture.Click)!;
        var cancelled = RegionProbe.Click(state, frame, cancel, MouseGesture.Click)!;

        Assert.Equal(4, fields.Count);
        Assert.Contains("Account", fields[2].Text, StringComparison.Ordinal);
        Assert.Equal(ProfileFormField.Email, focused.State.Form!.Field);
        Assert.IsType<TestProfileEffect>(Assert.Single(tested.Effects));
        Assert.IsType<UpdateProfileEffect>(Assert.Single(saved.Effects));
        Assert.Null(cancelled.State.Form);
    }

    [Fact]
    public void GivenATestResultAndANote_WhenBuildingTheFrame_ThenTheBoxShowsThem()
    {
        var failed = Editing() with { Form = Editing().Form! with { TestResult = "Failed: 401", TestFailed = true } };
        var running = Editing() with { Form = Editing().Form! with { Testing = true } };

        Assert.Contains(FrameBuilder.Build(failed, 80, 24, _keymap), line => line.Contains("Failed: 401"));
        Assert.Contains(FrameBuilder.Build(running, 80, 24, _keymap), line => line.Contains("Testing the connection"));
    }

    [Fact]
    public void GivenAnyTerminalSize_WhenBuildingTheEditFrame_ThenItNeverThrows()
    {
        for (var width = 0; width <= 40; width++)
        {
            for (var height = 0; height <= 14; height++)
            {
                FrameBuilder.BuildStyled(Editing(), width, height, _keymap);
            }
        }
    }

    private static AppState Editing()
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var catalog = ProfileCatalog.Empty.With(profile);

        return AppState.Initial with { Mode = InputMode.Insert, Profiles = catalog, Form = ProfileForm.For(profile) };
    }

    private static AppState Sample(ProfileFormField field, string? error)
    {
        var form = ProfileForm.Start with { Field = field, Token = new SecretText(Token), Error = error };

        return AppState.Initial with { Mode = InputMode.Insert, Form = form };
    }
}
