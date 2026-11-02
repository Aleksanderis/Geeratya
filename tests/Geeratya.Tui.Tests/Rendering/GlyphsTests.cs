using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

public class GlyphsTests
{
    [Fact]
    public void GivenEveryShellGlyph_WhenConvertingToAscii_ThenEachBecomesOneAsciiCharacter()
    {
        const string drawn = "‹›↑▾…·○◐●─━│┃┌┐└┘├┤┬┴┼┏┓┗┛┣┫╭╮╰╯";

        var ascii = Glyphs.ToAscii(drawn);

        Assert.Equal(drawn.Length, ascii.Length);
        Assert.All(ascii, character => Assert.True(character < 0x80));
    }

    [Fact]
    public void GivenIssueContent_WhenConvertingToAscii_ThenOtherTextIsKept()
    {
        Assert.Equal("日本語 é plain", Glyphs.ToAscii("日本語 é plain"));
    }
}
