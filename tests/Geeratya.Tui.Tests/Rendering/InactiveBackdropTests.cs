using Geeratya.Core.Text;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class InactiveBackdropTests
{
    [Fact]
    public void GivenAFocusedAndSelectedView_WhenMadeInactive_ThenNoFocusOrSelectionMarkIsLeftAndEverythingIsDim()
    {
        var line = new StyledLine(
        [
            new StyledSpan("To Do", TextRole.ChipFocus),
            new StyledSpan("╭", TextRole.FrameFocus),
            new StyledSpan("A-1", TextRole.Selected, SpanGround.Selection | SpanGround.Cursor),
            new StyledSpan("A-2", TextRole.Body, SpanGround.Range),
        ]);

        var spans = OverlayBox.Inactive([line])[0].Spans;

        Assert.All(spans, span => Assert.Equal(SpanGround.Dim, span.Ground));
        Assert.DoesNotContain(
            spans,
            span => span.Role is TextRole.ChipFocus or TextRole.FrameFocus or TextRole.Selected);
    }

    [Fact]
    public void GivenQuietSpans_WhenMadeInactive_ThenTheirRolesPassThrough()
    {
        var line = new StyledLine([new StyledSpan("a", TextRole.Muted), new StyledSpan("b", TextRole.Body)]);

        var spans = OverlayBox.Inactive([line])[0].Spans;

        Assert.Equal([TextRole.Muted, TextRole.Body], spans.Select(span => span.Role));
    }
}
