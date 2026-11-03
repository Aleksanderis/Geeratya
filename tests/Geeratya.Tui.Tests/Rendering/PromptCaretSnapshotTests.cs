using Geeratya.Core.Forms;
using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The single-line prompts with the caret inside the text (a bar between two characters) at the three snapshot sizes.
/// To refresh after an intended change, run with <c>GEERATYA_UPDATE_SNAPSHOTS=1</c> and review the diff.
/// </summary>
[Trait("Os", "Independent")]
public class PromptCaretSnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenTheJumpPromptWithTheCaretInside_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var state = JumpOverlaySnapshotTests.Sample("gcm") with { Jump = new JumpState("gcm", 0, null) { Back = 2 } };

        AssertSnapshot(state, $"prompt-jump-caret-{width}x{height}.txt", width, height);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenThePaletteWithTheCaretInside_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var state = PaletteOverlayTests.Sample("jql project") with { PaletteBack = 3 };

        AssertSnapshot(state, $"prompt-palette-caret-{width}x{height}.txt", width, height);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenThePickerWithTheCaretInside_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var sample = SourcePickerSnapshotTests.Sample("sprint");
        var state = sample with { Picker = sample.Picker! with { Back = 3 } };

        AssertSnapshot(state, $"prompt-picker-caret-{width}x{height}.txt", width, height);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenAFormTextFieldWithTheCaretInside_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var field = FormField.Create("summary", "Summary", FormFieldKind.Text, false, [])
            .WithInitial(FormValue.OfText("Fix the login redirect")) with { Back = 9 };
        var form = FormOverlaySnapshotTests.Sample().Session.Tabs[0].Form! with { Fields = [field], Focus = 0 };

        AssertSnapshot(FormOverlaySnapshotTests.WithForm(form), $"prompt-form-caret-{width}x{height}.txt", width, height);
    }

    [Fact]
    public void GivenALongPaletteTextWithTheCaretNearItsStart_WhenBuildingTheFrame_ThenTheCaretStaysInView()
    {
        var text = string.Concat(Enumerable.Repeat("abcdefghij ", 12));
        var state = PaletteOverlayTests.Sample(text) with { PaletteBack = text.Length - 3 };

        var lines = FrameBuilder.Build(state, 40, 10, _keymap);

        Assert.Contains(lines, line => line.Contains("abc|defghij", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.Equal(40, DisplayWidth.Of(line)));
    }

    [Fact]
    public void GivenAWideTextWithTheCaretInside_WhenBuildingNarrowFrames_ThenNoLineIsTooWide()
    {
        var state = JumpOverlaySnapshotTests.Sample("日本語日本語日本語") with
        {
            Jump = new JumpState("日本語日本語日本語", 0, null) { Back = 4 },
        };

        for (var width = 10; width <= 40; width++)
        {
            var lines = FrameBuilder.Build(state, width, 10, _keymap);

            Assert.All(lines, line => Assert.Equal(width, DisplayWidth.Of(line)));
        }
    }

    private static void AssertSnapshot(AppState state, string name, int width, int height)
    {
        var actual = string.Join('\n', FrameBuilder.Build(state, width, height, _keymap)) + "\n";

        Assert.Equal(SnapshotFile.Expect(name, actual), actual);
    }
}
