using Geeratya.Core.Browser;

namespace Geeratya.Core.Tests.Browser;

[Trait("Os", "Independent")]
public class BrowserUrlCheckTests
{
    [Fact]
    public void GivenHttpsOnRequiredHost_WhenChecking_ThenAccepted()
    {
        var result = BrowserUrlCheck.Check("https://Work.atlassian.net/browse/A-1", "work.atlassian.net");

        Assert.Equal("https://work.atlassian.net/browse/A-1", result.Url?.AbsoluteUri);
    }

    [Fact]
    public void GivenAnyHostAllowed_WhenChecking_ThenOtherHttpsHostAccepted()
    {
        Assert.NotNull(BrowserUrlCheck.Check("https://wiki.example.com/page", null).Url);
    }

    [Theory]
    [InlineData("http://work.atlassian.net/browse/A-1")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("https://work.atlassian.net/a\u0007b")]
    [InlineData("https://work.atlassian.net/a\nb")]
    [InlineData("https://me:secret@work.atlassian.net/")]
    [InlineData("https://evil.example.com/browse/A-1")]
    [InlineData("https://work.atlassian.net:8443/browse/A-1")]
    public void GivenUnsafeUrl_WhenChecking_ThenRefusedWithReason(string text)
    {
        var result = BrowserUrlCheck.Check(text, "work.atlassian.net");

        Assert.Null(result.Url);
        Assert.False(string.IsNullOrEmpty(result.Reason));
    }
}
