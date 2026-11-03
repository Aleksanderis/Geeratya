using System.Globalization;
using Geeratya.Core.Cache;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class SprintSourceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Theory]
    [InlineData("7:active", 7, null)]
    [InlineData("7:42", 7, 42L)]
    public void GivenWrittenForm_WhenParsed_ThenBoardAndSprintAreRead(string text, long board, long? sprint)
    {
        Assert.True(SprintSource.TryParse(text, out var source));
        Assert.Equal(new SprintSource(board, sprint), source);
        Assert.Equal(text, source.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("7")]
    [InlineData("7:")]
    [InlineData("x:1")]
    [InlineData("7:-1")]
    [InlineData("7:1:2")]
    [InlineData("7:Active")]
    public void GivenAnythingElse_WhenParsed_ThenItIsRejected(string text)
    {
        Assert.False(SprintSource.TryParse(text, out _));
    }

    [Fact]
    public void GivenSameSprintOnTwoViews_WhenKeyed_ThenKeysAreEqualAndDifferFromOtherSprintsAndKinds()
    {
        var first = Definition(ViewSourceKind.Sprint, "7:42");
        var again = Definition(ViewSourceKind.Sprint, "7:42") with { Layout = ViewLayout.Columns, Title = "Other" };

        Assert.Equal(CacheKey.For(first), CacheKey.For(again));
        Assert.NotEqual(CacheKey.For(first), CacheKey.For(Definition(ViewSourceKind.Sprint, "7:43")));
        Assert.NotEqual(CacheKey.For(first), CacheKey.For(Definition(ViewSourceKind.Sprint, "7:active")));
        Assert.NotEqual(
            CacheKey.For(Definition(ViewSourceKind.ScrumBacklog, "7")),
            CacheKey.For(Definition(ViewSourceKind.Backlog, "7")));
    }

    [Theory]
    [InlineData(ViewSourceKind.Sprint, "7:42")]
    [InlineData(ViewSourceKind.ScrumBacklog, "7")]
    public void GivenSprintSource_WhenOpened_ThenTheFirstPageIsRequestedInTheListLayout(
        ViewSourceKind source,
        string value)
    {
        var result = _reducer.Reduce(Start(), new ViewOpenRequested(Definition(source, value)));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        Assert.Equal(source, effect.Definition.Source);
        Assert.Equal(value, effect.Definition.SourceValue);
        Assert.Equal(ViewLayout.List, result.State.Session.Tabs[0].Definition.Layout);
        Assert.Null(effect.Cursor);
    }

    [Fact]
    public void GivenLoadedSprintTab_WhenSwitchingSprint_ThenOldWorkIsCancelledAndTheNewSprintLoads()
    {
        var loaded = Loaded(Definition(ViewSourceKind.Sprint, "7:42"), 3);
        var tab = loaded.Session.Tabs[0];

        var result = _reducer.Reduce(loaded, new SprintSwitched(tab.Id, new SprintSource(7, 43), "Sprint 43"));

        var switched = result.State.Session.Tabs[0];
        Assert.Equal("7:43", switched.Definition.SourceValue);
        Assert.Equal("Sprint 43", switched.Definition.Title);
        Assert.Empty(switched.Rows);
        Assert.Null(switched.SelectedKey);
        Assert.Equal(new Loading(LoadReason.Open), switched.Load);
        Assert.NotEqual(tab.RequestId, switched.RequestId);
        Assert.Equal(new CancelTabEffect(tab.Id), result.Effects[0]);
        var load = Assert.IsType<LoadViewPageEffect>(result.Effects[1]);
        Assert.Equal(switched.RequestId, load.RequestId);
        Assert.Equal("7:43", load.Definition.SourceValue);
    }

    [Fact]
    public void GivenSwitchedSprint_WhenTheOldSprintReplyArrives_ThenItIsIgnored()
    {
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(Definition(ViewSourceKind.Sprint, "7:42")));
        var tab = opened.State.Session.Tabs[0];
        var switched = _reducer.Reduce(opened.State, new SprintSwitched(tab.Id, new SprintSource(7, 43), "S43"));

        var late = _reducer.Reduce(switched.State, new ViewPageLoaded(tab.Id, tab.RequestId, Page(5), _now));

        Assert.Empty(late.State.Session.Tabs[0].Rows);
        Assert.Equal(new Loading(LoadReason.Open), late.State.Session.Tabs[0].Load);
    }

    [Fact]
    public void GivenSwitchedSprint_WhenTheNewReplyArrives_ThenItsRowsShow()
    {
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(Definition(ViewSourceKind.Sprint, "7:42")));
        var tab = opened.State.Session.Tabs[0];
        var switched = _reducer.Reduce(opened.State, new SprintSwitched(tab.Id, SprintSource.Active(7), "Active"));
        var current = switched.State.Session.Tabs[0];

        var done = _reducer.Reduce(switched.State, new ViewPageLoaded(tab.Id, current.RequestId, Page(2), _now));

        Assert.Equal(2, done.State.Session.Tabs[0].Rows.Count);
        Assert.Equal("7:active", done.State.Session.Tabs[0].Definition.SourceValue);
    }

    [Fact]
    public void GivenSprintReply_WhenItsRequestIsCurrent_ThenTheSprintIsKeptAndAStaleReplyIsIgnored()
    {
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(Definition(ViewSourceKind.Sprint, "7:active")));
        var tab = opened.State.Session.Tabs[0];
        var sprint = new Sprint(7, "Sprint 7", SprintState.Active, null, null, null, null, 7);

        var stale = _reducer.Reduce(opened.State, new SprintLoaded(tab.Id, tab.RequestId + 1, sprint));
        var current = _reducer.Reduce(opened.State, new SprintLoaded(tab.Id, tab.RequestId, sprint));

        Assert.False(stale.State.Session.Tabs[0].SprintResolved);
        Assert.Equal(sprint, current.State.Session.Tabs[0].Sprint);
        Assert.True(current.State.Session.Tabs[0].SprintResolved);
    }

    [Fact]
    public void GivenLoadedSprint_WhenSwitchingSprint_ThenTheOldSprintIsForgotten()
    {
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(Definition(ViewSourceKind.Sprint, "7:42")));
        var tab = opened.State.Session.Tabs[0];
        var sprint = new Sprint(42, "Old", SprintState.Closed, null, null, null, null, 7);
        var loaded = _reducer.Reduce(opened.State, new SprintLoaded(tab.Id, tab.RequestId, sprint));

        var switched = _reducer.Reduce(loaded.State, new SprintSwitched(tab.Id, new SprintSource(7, 43), "S43"));

        Assert.Null(switched.State.Session.Tabs[0].Sprint);
        Assert.False(switched.State.Session.Tabs[0].SprintResolved);
    }

    [Fact]
    public void GivenOpenIssueDetail_WhenSwitchingSprint_ThenTheDetailIsClosed()
    {
        var loaded = Loaded(Definition(ViewSourceKind.Sprint, "7:42"), 3);
        var tab = loaded.Session.Tabs[0];
        var withDetail = _reducer.Reduce(loaded, new IssueDetailOpenRequested(tab.Id, "A-1", tab.Rows[0])).State;
        Assert.NotEmpty(withDetail.Session.Tabs[0].Detail);

        var result = _reducer.Reduce(withDetail, new SprintSwitched(tab.Id, new SprintSource(7, 43), "S43"));

        Assert.Empty(result.State.Session.Tabs[0].Detail);
    }

    [Fact]
    public void GivenSameSprint_WhenSwitching_ThenNothingChanges()
    {
        var loaded = Loaded(Definition(ViewSourceKind.Sprint, "7:42"), 3);

        var tab = loaded.Session.Tabs[0];

        var result = _reducer.Reduce(loaded, new SprintSwitched(tab.Id, new SprintSource(7, 42), "x"));

        Assert.Same(loaded, result.State);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenNonSprintTabOrUnknownTab_WhenSwitching_ThenNothingChanges()
    {
        var board = Loaded(Definition(ViewSourceKind.Board, "7"), 3);

        var tab = board.Session.Tabs[0];

        var onBoard = _reducer.Reduce(board, new SprintSwitched(tab.Id, new SprintSource(7, 1), "x"));
        var unknown = _reducer.Reduce(board, new SprintSwitched(new TabId(999), new SprintSource(7, 1), "x"));

        Assert.Same(board, onBoard.State);
        Assert.Same(board, unknown.State);
    }

    [Fact]
    public void GivenSprintTab_WhenAskedForColumns_ThenTheLayoutChangesButOtherSourcesStayRefused()
    {
        var sprint = Loaded(Definition(ViewSourceKind.Sprint, "7:42"), 1);
        var backlog = Loaded(Definition(ViewSourceKind.ScrumBacklog, "7"), 1);

        var accepted = ViewReducer.SetLayout(sprint, ViewLayout.Columns);
        var refused = ViewReducer.SetLayout(backlog, ViewLayout.Columns);

        Assert.Equal(ViewLayout.Columns, accepted.State.Session.Tabs[0].Definition.Layout);
        Assert.Equal(ViewLayout.List, refused.State.Session.Tabs[0].Definition.Layout);
    }

    [Fact]
    public void GivenSprintTab_WhenTitled_ThenItReadsSprintAndName()
    {
        var definition = Definition(ViewSourceKind.Sprint, "7:42") with { Title = "Sprint 42" };

        Assert.Equal("Sprint: Sprint 42", TabTitles.For(definition));
    }

    private static ViewDefinition Definition(ViewSourceKind source, string value)
    {
        return new ViewDefinition(source, value, ViewLayout.List, "Scrum");
    }

    private static AppState Start()
    {
        return AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };
    }

    private AppState Loaded(ViewDefinition definition, int count)
    {
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(definition));
        var tab = opened.State.Session.Tabs[0];

        return _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, Page(count), _now)).State;
    }

    private static IssuePage Page(int count)
    {
        return new IssuePage(Enumerable.Range(1, count).Select(Issue).ToArray(), null, null);
    }

    private static IssueSummary Issue(int number)
    {
        return new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
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
}
