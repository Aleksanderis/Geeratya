using Geeratya.Core.Keymap;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

public class WhichKeyOverlaySnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);

    [Theory]
    [InlineData("Space", "popup-space-120x36.txt")]
    [InlineData("Space i", "popup-space-i-120x36.txt")]
    public void GivenKeysPending_WhenBuilding_ThenTheFrameMatchesTheSnapshot(string keys, string name)
    {
        var state = WhichKeyOverlayTests.Opened(keys);
        var actual = string.Join('\n', FrameBuilder.Build(state, 120, 36, _helpLines, _keymap));
        var expected = SnapshotFile.Expect(name, actual + "\n");

        Assert.Equal(expected.ReplaceLineEndings("\n").TrimEnd(), actual.TrimEnd());
    }
}
