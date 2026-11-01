using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class StatusBarTests
{
    [Fact]
    public void GivenEveryHint_WhenCheckingTheDefaultKeymap_ThenItBindsTheHintKeyToTheNamedAction()
    {
        var keymap = DefaultKeymap.Load();

        foreach (var hint in StatusBar.Hints)
        {
            Assert.Contains(
                keymap.Bindings,
                binding => binding.Mode == hint.Mode && binding.Keys == hint.Key && binding.Action == hint.ActionId);
        }
    }

    [Fact]
    public void GivenNormalMode_WhenRendering_ThenBadgeStartsAtColumnZeroAndHintsAreRightAligned()
    {
        var line = StatusBar.Render(AppState.Initial, 60);

        Assert.StartsWith(" NORMAL", line);
        Assert.EndsWith("q quit", line);
        Assert.Equal(60, line.Length);
    }

    [Fact]
    public void GivenCommandMode_WhenRendering_ThenTypedTextIsShownAfterTheColon()
    {
        var state = AppState.Initial with { Mode = InputMode.Command, PaletteText = "quit" };

        Assert.Contains("COMMAND :quit", StatusBar.Render(state, 60));
    }

    [Fact]
    public void GivenNarrowWidth_WhenRendering_ThenHintsAreDroppedBeforeTheStatusText()
    {
        var state = AppState.Initial with { Message = "Unknown command: x" };

        var line = StatusBar.Render(state, 30);

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
            var line = StatusBar.Render(FrameOptions.SampleState(mode), width);

            Assert.True(line.Length <= width);
        }
    }
}
