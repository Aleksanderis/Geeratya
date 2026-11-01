namespace Geeratya.Core.Tests;

public class ApplicationInfoTests
{
    [Fact]
    public void Version_comes_from_the_build()
    {
        Assert.Equal("0.1.0", ApplicationInfo.Version);
    }
}
