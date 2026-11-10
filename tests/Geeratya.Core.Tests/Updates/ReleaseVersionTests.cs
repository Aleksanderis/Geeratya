using Geeratya.Core.Updates;

namespace Geeratya.Core.Tests.Updates;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData(" V10.0.1-rc.1 ", "10.0.1-rc.1")]
    [InlineData("1.2.3+build.5", "1.2.3")]
    [InlineData("0.0.0-dev", "0.0.0-dev")]
    public void GivenAVersionText_WhenParsing_ThenTheTextLosesPrefixAndMetadata(string text, string expected)
    {
        Assert.Equal(expected, ReleaseVersion.Parse(text)!.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.x")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-a..b")]
    [InlineData("-1.2.3")]
    public void GivenNoVersion_WhenParsing_ThenNullComesBack(string? text)
    {
        Assert.Null(ReleaseVersion.Parse(text));
    }

    [Theory]
    [InlineData("1.2.4", "1.2.3")]
    [InlineData("1.10.0", "1.9.9")]
    [InlineData("2.0.0", "1.99.99")]
    [InlineData("1.2.3", "1.2.3-rc.1")]
    [InlineData("1.2.3-rc.2", "1.2.3-rc.1")]
    [InlineData("1.2.3-rc.10", "1.2.3-rc.9")]
    [InlineData("1.2.3-rc.1", "1.2.3-beta.5")]
    [InlineData("1.2.3-rc.1.1", "1.2.3-rc.1")]
    [InlineData("1.2.3-alpha", "1.2.3-1")]
    [InlineData("0.0.1", "0.0.0-dev")]
    public void GivenTwoVersions_WhenComparing_ThenTheFirstIsNewer(string newer, string older)
    {
        var first = ReleaseVersion.Parse(newer)!;
        var second = ReleaseVersion.Parse(older)!;

        Assert.True(first.IsNewerThan(second));
        Assert.False(second.IsNewerThan(first));
    }

    [Fact]
    public void GivenTheSameVersionWithAndWithoutPrefix_WhenComparing_ThenNeitherIsNewer()
    {
        var first = ReleaseVersion.Parse("v1.2.3")!;
        var second = ReleaseVersion.Parse("1.2.3+other")!;

        Assert.False(first.IsNewerThan(second));
        Assert.False(second.IsNewerThan(first));
    }

    [Fact]
    public void GivenTheDevelopmentMarker_WhenAsking_ThenItIsADevelopmentVersion()
    {
        Assert.True(ReleaseVersion.Parse(ReleaseVersion.DevelopmentText)!.IsDevelopment);
        Assert.False(ReleaseVersion.Parse("0.0.1")!.IsDevelopment);
    }
}
