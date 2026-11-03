using Geeratya.Core.Keymap;
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

        Assert.Equal(expected.ReplaceLineEndings("\n"), actual);
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

        Assert.Contains("INSERT [3/4] Account email", status, StringComparison.Ordinal);
        Assert.DoesNotContain("_", status, StringComparison.Ordinal);
        Assert.Contains(lines, line => line.Contains("> ", StringComparison.Ordinal) && line.Contains('│'));
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

    private static AppState Sample(ProfileFormField field, string? error)
    {
        var form = ProfileForm.Start with { Field = field, Token = new SecretText(Token), Error = error };

        return AppState.Initial with { Mode = InputMode.Insert, Form = form };
    }
}
