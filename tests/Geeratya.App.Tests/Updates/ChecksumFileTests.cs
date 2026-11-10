using Geeratya.App.Updates;

namespace Geeratya.App.Tests.Updates;

[Trait("Os", "Independent")]
public class ChecksumFileTests
{
    [Theory]
    [InlineData("ABC123  geeratya-1.0.0-linux-x64.tar.gz\n", "abc123")]
    [InlineData("abc123 *geeratya-1.0.0-linux-x64.tar.gz\n", "abc123")]
    [InlineData("ff00  other.zip\nabc123  geeratya-1.0.0-linux-x64.tar.gz", "abc123")]
    public void GivenALineForTheFile_WhenReading_ThenTheLowerCaseHashComesBack(string text, string expected)
    {
        Assert.Equal(expected, ChecksumFile.HashOf(text, "geeratya-1.0.0-linux-x64.tar.gz"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc123  other.zip\n")]
    [InlineData("abc123  xgeeratya-1.0.0-linux-x64.tar.gz\n")]
    public void GivenNoLineForTheFile_WhenReading_ThenNullComesBack(string text)
    {
        Assert.Null(ChecksumFile.HashOf(text, "geeratya-1.0.0-linux-x64.tar.gz"));
    }
}
