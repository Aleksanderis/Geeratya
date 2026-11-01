namespace Geeratya.Core.Tests;

[Trait("Os", "Independent")]
public class ApplicationInfoTests
{
    [Fact]
    public void GivenBuild_WhenReadingVersion_ThenItIsTheBuildVersionNotTheFallback()
    {
        Assert.False(string.IsNullOrWhiteSpace(ApplicationInfo.Version));
        Assert.NotEqual("0.0.0", ApplicationInfo.Version);
    }
}
