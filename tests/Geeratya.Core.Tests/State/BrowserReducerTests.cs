using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.State;

public class BrowserReducerTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenBrowseLinkOnActiveSite_WhenRequested_ThenOpenEffectWithExactUrl()
    {
        var result = _reducer.Reduce(WithProfile(), new OpenUrlRequested("https://work.atlassian.net/browse/ABC-1", false));

        var effect = Assert.IsType<OpenUrlEffect>(Assert.Single(result.Effects));

        Assert.Equal("https://work.atlassian.net/browse/ABC-1", effect.Url.AbsoluteUri);
    }

    [Fact]
    public void GivenRemoteLink_WhenRequested_ThenOtherHttpsHostIsAllowed()
    {
        var result = _reducer.Reduce(WithProfile(), new OpenUrlRequested("https://wiki.example.com/p", true));

        Assert.IsType<OpenUrlEffect>(Assert.Single(result.Effects));
    }

    [Theory]
    [InlineData("https://evil.example.com/x", false)]
    [InlineData("http://work.atlassian.net/x", false)]
    [InlineData("javascript:alert(1)", true)]
    [InlineData("file:///etc/passwd", true)]
    public void GivenUnsafeLink_WhenRequested_ThenNoEffectAndStatusMessage(string url, bool remote)
    {
        var result = _reducer.Reduce(WithProfile(), new OpenUrlRequested(url, remote));

        Assert.Empty(result.Effects);
        Assert.StartsWith("Cannot open the link", result.State.Message);
    }

    [Fact]
    public void GivenNoActiveSite_WhenBrowseLinkRequested_ThenRefused()
    {
        var result = _reducer.Reduce(AppState.Initial, new OpenUrlRequested("https://work.atlassian.net/x", false));

        Assert.Empty(result.Effects);
        Assert.Contains("no active site", result.State.Message);
    }

    [Fact]
    public void GivenFailure_WhenFinished_ThenStatusLineShowsReason()
    {
        var result = _reducer.Reduce(AppState.Initial, new UrlOpenFinished("no browser opener found: copy the URL"));

        Assert.Equal("Cannot open the link: no browser opener found: copy the URL", result.State.Message);
    }

    [Fact]
    public void GivenSuccess_WhenFinished_ThenStatusLineConfirms()
    {
        var result = _reducer.Reduce(AppState.Initial, new UrlOpenFinished(null));

        Assert.Equal("Opened in the browser", result.State.Message);
    }

    private static AppState WithProfile()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work) };
    }
}
