using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class StatusBarTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenEveryHint_WhenCheckingTheDefaultKeymap_ThenItBindsTheNamedActionInTheHintMode()
    {
        foreach (var hint in StatusBar.Hints)
        {
            Assert.NotNull(_keymap.FindKeys(hint.Mode, hint.ActionId));
            Assert.NotNull(ActionCatalog.FindTitle(hint.ActionId));
        }
    }

    [Fact]
    public void GivenNormalMode_WhenRendering_ThenBadgeStartsAtColumnZeroAndHintsAreRightAligned()
    {
        var line = StatusBar.Render(AppState.Initial, 60, _keymap);

        Assert.StartsWith(" NORMAL", line);
        Assert.EndsWith("q quit", line);
        Assert.Equal(60, line.Length);
    }

    [Fact]
    public void GivenCommandMode_WhenRendering_ThenTypedTextIsShownAfterTheColon()
    {
        var state = AppState.Initial with { Mode = InputMode.Command, PaletteText = "quit" };

        Assert.Contains("COMMAND :quit", StatusBar.Render(state, 60, _keymap));
    }

    [Fact]
    public void GivenNarrowWidth_WhenRendering_ThenHintsAreDroppedBeforeTheStatusText()
    {
        var state = AppState.Initial with { Message = "Unknown command: x" };

        var line = StatusBar.Render(state, 30, _keymap);

        Assert.DoesNotContain("quit", line);
        Assert.Contains("Unknown command: x", line);
        Assert.Equal(30, line.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void GivenTinyWidth_WhenRendering_ThenLineNeverExceedsTheWidth(int width)
    {
        foreach (var mode in Enum.GetValues<InputMode>())
        {
            var line = StatusBar.Render(FrameOptions.SampleState(mode), width, _keymap);

            Assert.True(line.Length <= width);
        }
    }

    [Fact]
    public void GivenNarrowWidth_WhenConfirming_ThenTheQuestionIsCutAndTheAnswerKeysStay()
    {
        var state = FrameOptions.SampleState(InputMode.Confirm);

        var line = StatusBar.Render(state, 30, _keymap);

        Assert.EndsWith("(y/n)", line.TrimEnd());
        Assert.Equal(30, line.Length);
    }

    [Fact]
    public void GivenARemappedQuitKey_WhenRenderingHints_ThenTheHintShowsTheNewKey()
    {
        var keymap = new KeymapTable([new KeyBinding(InputMode.Normal, "Ctrl-q", ActionIds.Quit)]);

        Assert.Equal(["Ctrl-q quit"], StatusBar.HintsFor(InputMode.Normal, keymap));
    }

    [Fact]
    public void GivenAHintActionUnbound_WhenRenderingHints_ThenItIsLeftOut()
    {
        var keymap = new KeymapTable([new KeyBinding(InputMode.Normal, "q", ActionIds.Quit)]);

        Assert.Equal(["q quit"], StatusBar.HintsFor(InputMode.Normal, keymap));
    }

    [Fact]
    public void GivenARemappedConfirmKey_WhenConfirming_ThenTheSuffixShowsTheNewKeys()
    {
        var keymap = new KeymapTable(
        [
            new KeyBinding(InputMode.Confirm, "Y", ActionIds.ConfirmYes),
            new KeyBinding(InputMode.Confirm, "n", ActionIds.ConfirmNo),
        ]);

        var line = StatusBar.Render(FrameOptions.SampleState(InputMode.Confirm), 80, keymap);

        Assert.Contains("(Y/n)", line);
    }

    [Fact]
    public void GivenDefaultKeymap_WhenRenderingHelpHints_ThenCloseShowsEscNotQ()
    {
        Assert.Contains("Esc close", StatusBar.HintsFor(InputMode.Help, _keymap));
    }
}
