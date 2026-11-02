using System.Globalization;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class ViewPagingTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenJqlCommand_WhenSubmitted_ThenATabOpensWithTheQueryAsTyped()
    {
        var result = Submit("jql project = GCM AND text ~ \"a: b\" ORDER BY created");

        var tab = Assert.Single(result.State.Session.Tabs);
        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        Assert.Equal(ViewSourceKind.Jql, tab.Definition.Source);
        Assert.Equal("project = GCM AND text ~ \"a: b\" ORDER BY created", tab.Definition.SourceValue);
        Assert.Equal(ViewLayout.List, tab.Definition.Layout);
        Assert.Equal(LoadReason.Open, effect.Reason);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Theory]
    [InlineData("jql")]
    [InlineData("jql   ")]
    public void GivenJqlCommandWithoutQuery_WhenSubmitted_ThenUsageIsShownAndNothingOpens(string text)
    {
        var result = Submit(text);

        Assert.Empty(result.State.Session.Tabs);
        Assert.Empty(result.Effects);
        Assert.Equal("Usage: :jql <query>", result.State.Message);
    }

    [Fact]
    public void GivenNoProfile_WhenSubmittingJql_ThenNothingOpensAndTheMessageSaysSo()
    {
        var result = Submit("jql project = A", AppState.Initial);

        Assert.Empty(result.State.Session.Tabs);
        Assert.Empty(result.Effects);
        Assert.Contains(":profile add", result.State.Message);
    }

    [Fact]
    public void GivenSavedFilterAndJql_WhenAskingForTheQuery_ThenFilterIsWrappedAndBoardsHaveNone()
    {
        var jql = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "t");
        var filter = jql with { Source = ViewSourceKind.SavedFilter, SourceValue = "10001" };

        Assert.Equal("project = A", ViewQuery.JqlFor(jql));
        Assert.Equal("filter = 10001", ViewQuery.JqlFor(filter));
        Assert.Null(ViewQuery.JqlFor(jql with { Source = ViewSourceKind.Board }));
        Assert.Null(ViewQuery.JqlFor(jql with { Source = ViewSourceKind.Backlog }));
    }

    [Fact]
    public void GivenSelectionFarFromTheEnd_WhenMoving_ThenNoPageIsRequested()
    {
        var state = LoadedFirstPage(100, 20);
        var id = state.Session.Tabs[0].Id;

        var result = _reducer.Reduce(state, new SelectionMoved(id, 50));

        Assert.Empty(result.Effects);
        Assert.Equal(50, result.State.Session.Tabs[0].Selected);
        Assert.Equal("A-51", result.State.Session.Tabs[0].SelectedKey);
    }

    [Fact]
    public void GivenSelectionWithinOneScreenOfTheEnd_WhenMoving_ThenTheNextPageIsRequestedOnce()
    {
        var state = LoadedFirstPage(100, 20);
        var id = state.Session.Tabs[0].Id;

        var near = _reducer.Reduce(state, new SelectionMoved(id, 80));
        var again = _reducer.Reduce(near.State, new SelectionMoved(id, 90));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(near.Effects));
        var tab = near.State.Session.Tabs[0];
        Assert.Equal(LoadReason.NextPage, effect.Reason);
        Assert.Equal(new PageCursor("n2"), effect.Cursor);
        Assert.Equal(tab.RequestId, effect.RequestId);
        Assert.Equal(new Loading(LoadReason.NextPage), tab.Load);
        Assert.Empty(again.Effects);
    }

    [Fact]
    public void GivenLastPageLoaded_WhenMovingToTheEnd_ThenNothingIsRequested()
    {
        var state = LoadedFirstPage(30, 20, null);
        var id = state.Session.Tabs[0].Id;

        var result = _reducer.Reduce(state, new SelectionMoved(id, 29));

        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenTallViewportAndMoreRows_WhenThePageArrives_ThenTheNextPageFollowsWithoutInput()
    {
        var state = LoadedFirstPage(100, 150);

        var tab = state.Session.Tabs[0];
        Assert.Equal(new Loading(LoadReason.NextPage), tab.Load);
    }

    [Fact]
    public void GivenHiddenTab_WhenItsPageArrivesNearTheEnd_ThenNoPageIsRequested()
    {
        var first = Open(Start(), "project = A");
        var firstTab = first.State.Session.Tabs[0];
        var second = _reducer.Reduce(first.State, new ViewOpenRequested(Definition("project = B")));
        var reply = new ViewPageLoaded(firstTab.Id, firstTab.RequestId, Page(1, new PageCursor("n")), _now);

        var result = _reducer.Reduce(second.State, reply);

        Assert.DoesNotContain(result.Effects, effect => effect is LoadViewPageEffect);
    }

    [Fact]
    public void GivenFailedNextPage_WhenTheSelectionMovesOnNearTheEnd_ThenNoRequestIsSentAgain()
    {
        var failed = FailedNextPage();
        var id = failed.Session.Tabs[0].Id;

        var moved = _reducer.Reduce(failed, new SelectionMoved(id, 90));

        Assert.Empty(moved.Effects);
        Assert.IsType<Failed>(moved.State.Session.Tabs[0].Load);
    }

    [Fact]
    public void GivenFailedNextPage_WhenRequestingExplicitly_ThenTheNextPageIsRetried()
    {
        var failed = FailedNextPage();
        var id = failed.Session.Tabs[0].Id;

        var retry = _reducer.Reduce(failed, new NextPageRequested(id));

        Assert.IsType<LoadViewPageEffect>(Assert.Single(retry.Effects));
    }

    [Fact]
    public void GivenEmptyFirstPage_WhenItArrivesWithoutToken_ThenTheTabIsIdleAndAsksForNothing()
    {
        var state = LoadedFirstPage(0, 20, null);

        Assert.Equal(LoadState.Idle, state.Session.Tabs[0].Load);
    }

    [Fact]
    public void GivenSelectionMovedOnUnknownTab_WhenReducing_ThenNothingChanges()
    {
        var state = LoadedFirstPage(10, 5);

        var result = _reducer.Reduce(state, new SelectionMoved(new TabId(999), 3));

        Assert.Same(state, result.State);
    }

    [Fact]
    public void GivenExpiredPageToken_WhenTheNextPageFails_ThenItRestartsFromTheFirstPageAndKeepsTheSelectionByKey()
    {
        var state = LoadedFirstPage(100, 20);
        var id = state.Session.Tabs[0].Id;
        var requested = _reducer.Reduce(state, new SelectionMoved(id, 85));
        var requestId = requested.State.Session.Tabs[0].RequestId;
        var expired = new JiraError(JiraErrorKind.PageExpired, 400, "expired");

        var restarted = _reducer.Reduce(requested.State, new ViewLoadFailed(id, requestId, expired, _now));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(restarted.Effects));
        var tab = restarted.State.Session.Tabs[0];
        Assert.Null(effect.Cursor);
        Assert.Equal(LoadReason.Retry, effect.Reason);
        Assert.Equal(new Loading(LoadReason.Retry), tab.Load);
        Assert.Equal(100, tab.Rows.Count);
        Assert.NotEqual(requestId, tab.RequestId);

        var reload = _reducer.Reduce(
            restarted.State,
            new ViewPageLoaded(id, tab.RequestId, Page(100, new PageCursor("fresh"), 41), _now));

        var reloaded = reload.State.Session.Tabs[0];
        Assert.Equal("A-86", reloaded.SelectedKey);
        Assert.Equal(45, reloaded.Selected);
        Assert.Equal(100, reloaded.Rows.Count);
        Assert.Equal(new PageCursor("fresh"), reloaded.Next);
    }

    [Fact]
    public void GivenExpiredTokenAnswerForSupersededRequest_WhenItArrives_ThenNothingRestarts()
    {
        var state = LoadedFirstPage(100, 20);
        var id = state.Session.Tabs[0].Id;
        var requested = _reducer.Reduce(state, new SelectionMoved(id, 85));
        var requestId = requested.State.Session.Tabs[0].RequestId;
        var expired = new JiraError(JiraErrorKind.PageExpired, 400, "expired");

        var result = _reducer.Reduce(requested.State, new ViewLoadFailed(id, requestId + 1, expired, _now));

        Assert.Empty(result.Effects);
        Assert.Same(requested.State, result.State);
    }

    [Fact]
    public void GivenOtherFailureOnNextPage_WhenItArrives_ThenRowsStayAndTheMessageComesFromTheTable()
    {
        var state = LoadedFirstPage(100, 20);
        var id = state.Session.Tabs[0].Id;
        var requested = _reducer.Reduce(state, new SelectionMoved(id, 85));
        var requestId = requested.State.Session.Tabs[0].RequestId;
        var error = new JiraError(JiraErrorKind.Unauthorized, 401, "raw");

        var failed = _reducer.Reduce(requested.State, new ViewLoadFailed(id, requestId, error, _now));

        var tab = failed.State.Session.Tabs[0];
        Assert.Empty(failed.Effects);
        Assert.Equal(100, tab.Rows.Count);
        Assert.Equal(new Failed(JiraErrorKind.Unauthorized, LoadErrorText.For(error), LoadReason.NextPage), tab.Load);
        Assert.True(tab.IsStale);
    }

    [Fact]
    public void GivenOpenChain_WhenPagesArriveUntilTheLast_ThenRowsAreAppendedInOrderAndLoadingStops()
    {
        var state = LoadedFirstPage(100, 30);
        var id = state.Session.Tabs[0].Id;
        var near = _reducer.Reduce(state, new SelectionMoved(id, 95));
        var requestId = near.State.Session.Tabs[0].RequestId;

        var second = _reducer.Reduce(near.State, new ViewPageLoaded(id, requestId, Page(40, null, 101), _now));

        var tab = second.State.Session.Tabs[0];
        Assert.Equal(140, tab.Rows.Count);
        Assert.Equal("A-140", tab.Rows[^1].Key);
        Assert.Equal("A-96", tab.SelectedKey);
        Assert.Null(tab.Next);
        Assert.Equal(LoadState.Idle, tab.Load);
        Assert.Empty(second.Effects);
    }

    [Theory]
    [InlineData(JiraErrorKind.Unauthorized, "credentials")]
    [InlineData(JiraErrorKind.Forbidden, "no access")]
    [InlineData(JiraErrorKind.NotFound, "no access")]
    [InlineData(JiraErrorKind.Timeout, "in time")]
    [InlineData(JiraErrorKind.Network, "Cannot reach")]
    [InlineData(JiraErrorKind.HostRefused, "Cannot reach")]
    [InlineData(JiraErrorKind.PageExpired, "expired")]
    [InlineData(JiraErrorKind.RateLimited, "rate limiting")]
    public void GivenErrorKind_WhenBuildingTheText_ThenItIsTheTableMessage(JiraErrorKind kind, string expected)
    {
        var text = LoadErrorText.For(new JiraError(kind, null, "Jira's own words"));

        Assert.Contains(expected, text);
        Assert.DoesNotContain("Jira's own words", text);
    }

    [Theory]
    [InlineData(JiraErrorKind.BadRequest)]
    [InlineData(JiraErrorKind.InvalidResponse)]
    [InlineData(JiraErrorKind.Unexpected)]
    public void GivenKindWithoutTableText_WhenBuildingTheText_ThenJirasMessageIsShown(JiraErrorKind kind)
    {
        var text = LoadErrorText.For(new JiraError(kind, null, "Error in the JQL Query: bad"));

        Assert.Equal("Error in the JQL Query: bad", text);
    }

    [Fact]
    public void GivenServerError_WhenBuildingTheText_ThenTheStatusAndTheRetryKeyAreNamed()
    {
        var text = LoadErrorText.For(new JiraError(JiraErrorKind.ServerError, 503, "raw body"));

        Assert.Equal("Jira returned an error (503). <space>r tries again.", text);
    }

    [Fact]
    public void GivenRateLimitWithWait_WhenBuildingTheText_ThenTheWaitIsIncluded()
    {
        var error = new JiraError(JiraErrorKind.RateLimited, 429, "slow", TimeSpan.FromSeconds(29.2));

        Assert.Contains("30 s", LoadErrorText.For(error));
        Assert.DoesNotContain(" s.", LoadErrorText.For(error with { RetryAfter = null }));
    }

    private static AppState Start()
    {
        var profiles = ProfileCatalog.Empty.With(_work);

        return AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
    }

    private static ViewDefinition Definition(string jql)
    {
        return new ViewDefinition(ViewSourceKind.Jql, jql, ViewLayout.List, jql);
    }

    private static IssuePage Page(int count, PageCursor? next, int firstNumber = 1)
    {
        var items = Enumerable.Range(firstNumber, count).Select(Issue).ToArray();

        return new IssuePage(items, next, null);
    }

    private static IssueSummary Issue(int number)
    {
        var status = new IssueStatus("1", "To Do", "new");
        var type = new IssueTypeSummary("10", "Task", 0, false);

        return new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
            $"A-{number}",
            $"Issue {number}",
            status,
            type,
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private AppState FailedNextPage()
    {
        var state = LoadedFirstPage(100, 20);
        var id = state.Session.Tabs[0].Id;
        var requested = _reducer.Reduce(state, new SelectionMoved(id, 85));
        var requestId = requested.State.Session.Tabs[0].RequestId;
        var error = new JiraError(JiraErrorKind.ServerError, 500, "boom");

        return _reducer.Reduce(requested.State, new ViewLoadFailed(id, requestId, error, _now)).State;
    }

    private ReduceResult Open(AppState state, string jql)
    {
        return _reducer.Reduce(state, new ViewOpenRequested(Definition(jql)));
    }

    private ReduceResult Submit(string text, AppState? start = null)
    {
        var state = (start ?? Start()) with { Mode = InputMode.Command, PaletteText = text };

        return _reducer.Reduce(state, new KeyPressed("Enter"));
    }

    /// <summary>A tab with the first page loaded and the viewport set; the cursor defaults to "n2".</summary>
    private AppState LoadedFirstPage(int rows, int viewport, string? next = "n2")
    {
        var opened = Open(Start(), "project = A");
        var tab = opened.State.Session.Tabs[0];
        var sized = _reducer.Reduce(opened.State, new ViewportChanged(viewport));
        var cursor = next is null ? null : new PageCursor(next);

        return _reducer.Reduce(sized.State, new ViewPageLoaded(tab.Id, tab.RequestId, Page(rows, cursor), _now)).State;
    }
}
