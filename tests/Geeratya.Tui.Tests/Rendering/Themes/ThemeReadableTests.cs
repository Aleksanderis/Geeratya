using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering.Themes;

[Trait("Os", "Independent")]
public class ThemeReadableTests
{
    private static readonly Rgb _black = new(0, 0, 0);
    private static readonly Rgb _white = new(255, 255, 255);
    private static readonly Rgb _nearBlack = new(20, 20, 20);
    private static readonly Rgb _grey = new(110, 110, 110);

    [Fact]
    public void GivenAPair_WhenAskingRepeatedly_ThenTheAnswerMatchesTheContrastRatio()
    {
        var theme = BuiltInThemes.Default;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.True(theme.IsReadable(_white, _black, 3.0));
            Assert.False(theme.IsReadable(_nearBlack, _black, 3.0));
        }
    }

    [Fact]
    public void GivenOnePair_WhenTheMinimumDiffers_ThenEachMinimumHasItsOwnAnswer()
    {
        var theme = BuiltInThemes.Default;
        var ratio = Contrast.Ratio(_grey, _black);

        Assert.True(theme.IsReadable(_grey, _black, ratio - 0.1));
        Assert.False(theme.IsReadable(_grey, _black, ratio + 0.1));
    }
}
