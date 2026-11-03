using Geeratya.Core.Keymap;
using Geeratya.Tui.Input;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace Geeratya.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class KeyTranslatorTests
{
    [Theory]
    [InlineData(KeyCode.Esc, "Esc")]
    [InlineData(KeyCode.Enter, "Enter")]
    [InlineData(KeyCode.CursorDown, "Down")]
    [InlineData(KeyCode.PageUp, "PageUp")]
    [InlineData(KeyCode.Space, "Space")]
    [InlineData(KeyCode.Backspace, "Backspace")]
    public void GivenNamedKey_WhenTranslating_ThenUsesKeymapSpelling(KeyCode code, string expected)
    {
        Assert.Equal(expected, KeyTranslator.Translate(new Key(code)));
    }

    [Fact]
    public void GivenLetterWithoutShift_WhenTranslating_ThenLowercase()
    {
        Assert.Equal("j", KeyTranslator.Translate(new Key(KeyCode.J)));
    }

    [Fact]
    public void GivenLetterWithShift_WhenTranslating_ThenUppercase()
    {
        Assert.Equal("G", KeyTranslator.Translate(new Key(KeyCode.G | KeyCode.ShiftMask)));
    }

    [Fact]
    public void GivenCharacterKeyFromUppercaseG_WhenTranslating_ThenUppercase()
    {
        Assert.Equal("G", KeyTranslator.Translate(new Key('G')));
    }

    [Fact]
    public void GivenCtrlLetter_WhenTranslating_ThenCtrlPrefixAndLowercase()
    {
        Assert.Equal("Ctrl-d", KeyTranslator.Translate(new Key(KeyCode.D | KeyCode.CtrlMask)));
    }

    [Fact]
    public void GivenCtrlSix_WhenTranslating_ThenCtrlCaret()
    {
        Assert.Equal("Ctrl-^", KeyTranslator.Translate(new Key(KeyCode.D6 | KeyCode.CtrlMask)));
    }

    [Fact]
    public void GivenQuestionMark_WhenTranslating_ThenTheCharacter()
    {
        Assert.Equal("?", KeyTranslator.Translate(new Key('?')));
    }

    [Theory]
    [InlineData(KeyCode.Esc)]
    [InlineData(KeyCode.CursorDown)]
    [InlineData(KeyCode.PageDown)]
    [InlineData(KeyCode.Home)]
    public void GivenNamedKey_WhenTranslating_ThenResultIsValidKeymapNotation(KeyCode code)
    {
        Assert.True(KeyNotation.IsValid(KeyTranslator.Translate(new Key(code))!));
    }
}
