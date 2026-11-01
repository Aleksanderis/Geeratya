using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class FrameBuilderTests
{
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(DefaultKeymap.Load());

    [Fact]
    public void GivenEveryModeAndManySizes_WhenBuilding_ThenLinesFillTheSizeExactly()
    {
        foreach (var mode in Enum.GetValues<InputMode>())
        {
            var state = FrameOptions.SampleState(mode);

            for (var width = 0; width <= 60; width++)
            {
                for (var height = 0; height <= 14; height++)
                {
                    AssertFills(FrameBuilder.Build(state, width, height, _helpLines), width, height);
                }
            }
        }
    }

    [Fact]
    public void GivenHelpScrolledPastTheEnd_WhenBuilding_ThenFrameStillFillsTheSize()
    {
        var state = AppState.Initial with { Mode = InputMode.Help, HelpScroll = 10_000 };

        AssertFills(FrameBuilder.Build(state, 40, 10, _helpLines), 40, 10);
    }

    [Fact]
    public void GivenWideCharactersInTheStatusText_WhenBuilding_ThenRowsKeepTheirWidth()
    {
        var state = AppState.Initial with { Message = "日本語日本語日本語日本語" };

        AssertFills(FrameBuilder.Build(state, 15, 5, _helpLines), 15, 5);
    }

    private static void AssertFills(IReadOnlyList<string> lines, int width, int height)
    {
        if (width == 0 || height == 0)
        {
            Assert.Empty(lines);

            return;
        }

        Assert.Equal(height, lines.Count);
        Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line)));
    }
}
