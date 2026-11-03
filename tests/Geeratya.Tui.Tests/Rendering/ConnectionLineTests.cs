using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Jira.Transport;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ConnectionLineTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly Profile _profile = new("work", "https://work.atlassian.net", "me@work.test");

    [Fact]
    public void GivenConnected_WhenBuildingTheStartScreen_ThenTheConnectedLineShowsBelowTheProfile()
    {
        var lines = FrameBuilder.Build(With(new Connected("Test User 1")), 80, 12, _keymap);

        var profile = lines.ToList().FindIndex(line => line.Contains("me@work.test"));

        Assert.Contains("Connected as Test User 1 on work.atlassian.net", lines[profile + 1]);
    }

    [Fact]
    public void GivenChecking_WhenBuildingTheStartScreen_ThenCheckingShowsAndNoLineOverflows()
    {
        var lines = FrameBuilder.Build(With(new Checking()), 40, 10, _keymap);

        Assert.Contains(lines, line => line.Contains("Checking work.atlassian.net..."));
        Assert.All(lines, line => Assert.True(line.Length <= 40));
    }

    [Fact]
    public void GivenRejected_WhenRenderingTheStatusLine_ThenTheHintShowsInPlaceOfAnEmptyMessage()
    {
        var failure = ConnectionText.Failure(new JiraError(JiraErrorKind.Unauthorized, 401, "x"), _profile.SiteUrl);

        var line = StatusBar.Render(With(failure), 120, _keymap);

        Assert.Contains("Token or email rejected", line);
    }

    [Fact]
    public void GivenConnectedAndAMessage_WhenRenderingTheStatusLine_ThenTheMessageWins()
    {
        var state = With(new Connected("Test User 1")) with { Message = "Switched." };

        Assert.Contains("Switched.", StatusBar.Render(state, 80, _keymap));
        Assert.DoesNotContain("Connected as", StatusBar.Render(state, 80, _keymap));
    }

    private static AppState With(ConnectionState connection)
    {
        var session = SiteSession.Empty(_profile) with { Connection = connection };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_profile), Session = session };
    }
}
