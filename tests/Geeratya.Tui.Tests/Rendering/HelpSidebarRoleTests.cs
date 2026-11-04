using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class HelpSidebarRoleTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenTheOpenHelp_WhenComposing_ThenTheActiveSidebarEntryIsHeadingAndTheOthersMutedNotSelected()
    {
        var body = Enumerable.Range(0, 24).Select(_ => StyledLine.Empty).ToList();
        var state = AppState.Initial with { Mode = InputMode.Help };
        var lines = HelpOverlay.Compose(body, state, _keymap, 100, 24);
        var spans = lines.SelectMany(line => line.Spans).ToList();

        Assert.Single(spans, span => span.Role == TextRole.Heading && span.Text.StartsWith("> ", StringComparison.Ordinal));
        Assert.Contains(spans, span => span.Role == TextRole.Muted && span.Text.StartsWith("  ", StringComparison.Ordinal));
        Assert.DoesNotContain(spans, span => span.Role == TextRole.Selected);
    }
}
