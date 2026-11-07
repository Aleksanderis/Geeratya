using Geeratya.Platform.Editors;

namespace Geeratya.Platform.Tests.Editors;

[Trait("Os", "Independent")]
public class EditorCommandLineTests
{
    [Theory]
    [InlineData("vim", new[] { "vim" })]
    [InlineData("code --wait", new[] { "code", "--wait" })]
    [InlineData("  nvim   -f ", new[] { "nvim", "-f" })]
    [InlineData("\"C:\\Program Files\\Ed\\ed.exe\" -w", new[] { "C:\\Program Files\\Ed\\ed.exe", "-w" })]
    [InlineData("vim -c 'set ft=markdown'", new[] { "vim", "-c", "set ft=markdown" })]
    [InlineData("ed \"\"", new[] { "ed", "" })]
    [InlineData("", new string[0])]
    [InlineData("   ", new string[0])]
    public void GivenAValue_WhenSplitting_ThenWordsFollowQuotesAndNoEscapes(string value, string[] expected)
    {
        Assert.Equal(expected, EditorCommandLine.Split(value));
    }
}
