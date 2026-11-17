using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The footers of the screens that used to rely on the status bar (Help, Messages, the context menu, the profile
/// manager and which-key, <c>docs/internal/ui-consistency.md</c>, component 6): typed chips, a key role on the key and
/// none when the screen is too small for them.
/// </summary>
[Trait("Os", "Independent")]
public class DialogFooterScreensTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenTheOpenHelp_WhenBuildingTheFrame_ThenTheFooterNamesMovePageFilterAndClose()
    {
        var text = Text(AppState.Initial with { Mode = InputMode.Help }, 100, 30);

        Assert.Contains("j/k move   Tab page   / filter   Esc close", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenALowHelp_WhenBuildingTheFrame_ThenThereIsNoFooterAndTheKeysStayOnTheStatusBar()
    {
        var text = Text(AppState.Initial with { Mode = InputMode.Help }, 100, 12);

        Assert.DoesNotContain("Tab page   / filter", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheMessages_WhenBuildingTheFrame_ThenTheFooterHasNoPageChips()
    {
        var text = Text(StatusMessages.OpenHistory(AppState.Initial with { Mode = InputMode.Help }), 100, 24);

        Assert.Contains("j/k move   Esc close", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Tab page   / filter", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheContextMenu_WhenBuildingTheFrame_ThenTheFooterNamesMoveRunAndClose()
    {
        var text = Text(ContextMenuOverlayTests.Sample(), 100, 30);

        Assert.Contains("Up/Down move   Enter run   Esc close", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheProfileManager_WhenBuildingTheFrame_ThenTheFooterNamesMoveAndSwitch()
    {
        var text = Text(ProfileManagerScreenTests.Sample(), 100, 24);

        Assert.Contains("Up/Down move   Enter switch", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnOpenWhichKey_WhenBuildingTheFrame_ThenTheFooterOffersCancelWithTheKeyRole()
    {
        var frame = FrameBuilder.BuildStyled(WhichKeyOverlayTests.Opened("Space i"), 120, 36, _keymap)!;
        var spans = frame.Body.SelectMany(line => line.Spans).ToList();

        Assert.Contains(spans, span => span.Role == TextRole.Key && span.Text == "Esc");
        Assert.Contains("cancel", string.Join('\n', frame.PlainLines()), StringComparison.Ordinal);
    }

    private static string Text(AppState state, int width, int height)
    {
        return string.Join('\n', FrameBuilder.BuildStyled(state, width, height, _keymap)!.PlainLines());
    }
}
