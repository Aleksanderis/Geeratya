using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public class OpenIssueInBrowserTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenSelectedRow_WhenPressingSpaceO_ThenOpenEffectWithTheBrowseUrlOfThatIssue()
    {
        var state = Move(Filled(3), 1);

        var result = Space(state, "o");

        var effect = Assert.IsType<OpenUrlEffect>(Assert.Single(result.Effects));

        Assert.Equal("https://work.atlassian.net/browse/A-2", effect.Url.AbsoluteUri);
        Assert.Equal("A-2", effect.Label);
    }

    [Fact]
    public void GivenNoTab_WhenPressingSpaceO_ThenStatusHintAndNoEffect()
    {
        var result = Space(Start(), "o");

        Assert.Empty(result.Effects);
        Assert.StartsWith("No issue selected", result.State.Message);
    }

    [Fact]
    public void GivenTabWithoutRows_WhenPressingSpaceO_ThenStatusHintAndNoEffect()
    {
        var result = Space(Open(Start()), "o");

        Assert.Empty(result.Effects);
        Assert.StartsWith("No issue selected", result.State.Message);
    }

    [Fact]
    public void GivenTypedKey_WhenRunningBrowse_ThenKeyIsUpperCasedAndOpened()
    {
        var result = Run(Start(), "browse gcm-12");

        var effect = Assert.IsType<OpenUrlEffect>(Assert.Single(result.Effects));

        Assert.Equal("https://work.atlassian.net/browse/GCM-12", effect.Url.AbsoluteUri);
    }

    [Fact]
    public void GivenBrowseWithoutKey_WhenRunning_ThenTheSelectedRowOpens()
    {
        var result = Run(Move(Filled(3), 2), "browse");

        var effect = Assert.IsType<OpenUrlEffect>(Assert.Single(result.Effects));

        Assert.Equal("https://work.atlassian.net/browse/A-3", effect.Url.AbsoluteUri);
    }

    [Theory]
    [InlineData("browse ../admin")]
    [InlineData("browse A-1?x=1")]
    [InlineData("browse A-1/../B")]
    [InlineData("browse 12")]
    public void GivenHostileKeyText_WhenRunningBrowse_ThenNoEffectAndRefusalMessage(string command)
    {
        var result = Run(Start(), command);

        Assert.Empty(result.Effects);
        Assert.Equal("Cannot open the link: not an issue key", result.State.Message);
    }

    [Fact]
    public void GivenNoActiveProfile_WhenRunningBrowse_ThenRefused()
    {
        var result = Run(AppState.Initial, "browse A-1");

        Assert.Empty(result.Effects);
        Assert.Contains("no active site", result.State.Message);
    }

    [Fact]
    public void GivenOpenedWithLabel_WhenFinished_ThenStatusNamesTheIssue()
    {
        var done = _reducer.Reduce(Start(), new UrlOpenFinished(null, "GCM-12"));
        var failed = _reducer.Reduce(Start(), new UrlOpenFinished("no browser opener found", "GCM-12"));

        Assert.Equal("Opened GCM-12 in the browser", done.State.Message);
        Assert.Equal("Cannot open the link: no browser opener found", failed.State.Message);
    }

    private static AppState Start()
    {
        var profiles = ProfileCatalog.Empty.With(_work);

        return AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
    }

    private static IssueSummary Issue(int number)
    {
        return new IssueSummary(
            number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"A-{number}",
            $"Issue {number}",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private AppState Open(AppState state)
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");

        return _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
    }

    private AppState Filled(int rows)
    {
        var state = Open(Start());
        var tab = state.Session.Tabs.Single();
        var page = new IssuePage(Enumerable.Range(1, rows).Select(Issue).ToArray(), null, null);

        return _reducer.Reduce(state, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    private AppState Move(AppState state, int index)
    {
        return _reducer.Reduce(state, new SelectionMoved(state.Session.Active!.Value, index)).State;
    }

    private ReduceResult Space(AppState state, string key)
    {
        return _reducer.Reduce(_reducer.Reduce(state, new KeyPressed("Space")).State, new KeyPressed(key));
    }

    private ReduceResult Run(AppState state, string command)
    {
        var result = _reducer.Reduce(state, new KeyPressed(":"));

        foreach (var character in command)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(character == ' ' ? "Space" : character.ToString()));
        }

        return _reducer.Reduce(result.State, new KeyPressed("Enter"));
    }
}
