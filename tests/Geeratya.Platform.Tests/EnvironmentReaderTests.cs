namespace Geeratya.Platform.Tests;

[Trait("Os", "Independent")]
public class EnvironmentReaderTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GivenEmptyOrMissingValue_WhenGetting_ThenReturnsNull(string? value)
    {
        var reader = new EnvironmentReader(_ => value);

        Assert.Null(reader.Get("NO_COLOR"));
    }

    [Fact]
    public void GivenValue_WhenGetting_ThenReturnsIt()
    {
        var reader = new EnvironmentReader(name => $"value of {name}");

        Assert.Equal("value of TERM", reader.Get("TERM"));
    }
}
