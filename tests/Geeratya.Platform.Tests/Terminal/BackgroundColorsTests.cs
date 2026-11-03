using Geeratya.Platform.Terminal;

namespace Geeratya.Platform.Tests.Terminal;

public class BackgroundColorsTests
{
    [Theory]
    [InlineData("\u001b]11;rgb:ffff/ffff/ffff\u001b\\", true)]
    [InlineData("\u001b]11;rgb:0000/0000/0000\u0007", false)]
    [InlineData("]11;rgb:1e/1e/2e\\", false)]
    [InlineData("]11;rgb:f/f/f\\", true)]
    [InlineData("]11;rgba:eeee/eeee/eeee/ffff\\", true)]
    public void GivenAnOsc11Answer_WhenParsing_ThenItSaysLightOrDark(string answer, bool expected)
    {
        Assert.Equal(expected, BackgroundColors.ParseOsc11(answer));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nothing here")]
    [InlineData("]11;rgb:zz/00/00\\")]
    [InlineData("]11;rgb:ff/ff\\")]
    [InlineData("]11;rgb:fffff/ff/ff\\")]
    public void GivenAnAnswerWithoutAColour_WhenParsing_ThenItIsUnknown(string answer)
    {
        Assert.Null(BackgroundColors.ParseOsc11(answer));
    }

    [Theory]
    [InlineData("15;0", false)]
    [InlineData("0;15", true)]
    [InlineData("0;default;15", true)]
    [InlineData("15;7", true)]
    [InlineData("15;8", false)]
    [InlineData("7;6", false)]
    public void GivenColorFgBg_WhenParsing_ThenTheBackgroundIndexDecides(string value, bool expected)
    {
        Assert.Equal(expected, BackgroundColors.ParseColorFgBg(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("15;default")]
    [InlineData("0;99")]
    public void GivenNoUsableColorFgBg_WhenParsing_ThenItIsUnknown(string? value)
    {
        Assert.Null(BackgroundColors.ParseColorFgBg(value));
    }
}
