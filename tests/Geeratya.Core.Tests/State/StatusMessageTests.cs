using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public sealed class StatusMessageTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Theory]
    [InlineData(StatusSeverity.Info, 5)]
    [InlineData(StatusSeverity.Warning, 15)]
    [InlineData(StatusSeverity.Error, 30)]
    public void GivenAMessage_WhenTheTicksPassItsLifetime_ThenItEndsExactlyThen(StatusSeverity severity, int seconds)
    {
        var posted = Post("Something", severity);

        var before = _reducer.Reduce(posted, new Tick(_start.AddSeconds(seconds - 1))).State;
        var after = _reducer.Reduce(before, new Tick(_start.AddSeconds(seconds))).State;

        Assert.Equal("Something", before.Status!.Text);
        Assert.Equal(severity, before.Status.Severity);
        Assert.Null(after.Status);
        Assert.Null(after.Message);
    }

    [Fact]
    public void GivenAnExpiredMessage_WhenReadingTheHistory_ThenItStaysThere()
    {
        var posted = Post("Gone soon", StatusSeverity.Info);

        var expired = _reducer.Reduce(posted, new Tick(_start.AddSeconds(6))).State;

        Assert.Null(expired.Status);
        Assert.Equal("Gone soon", Assert.Single(expired.StatusHistory).Text);
    }

    [Fact]
    public void GivenAShowingMessage_WhenKeysAreTyped_ThenItStays()
    {
        var posted = Post("Write failed", StatusSeverity.Error);

        var typed = _reducer.Reduce(posted, new KeyPressed("j", _start.AddSeconds(1))).State;
        var counted = _reducer.Reduce(typed, new KeyPressed("2", _start.AddSeconds(2))).State;

        Assert.Equal("Write failed", counted.Status!.Text);
        Assert.Equal("Write failed", counted.Message);
    }

    [Fact]
    public void GivenTheSameTextWhileShowing_WhenPostedAgain_ThenTheTimerIsNotRenewedAndHistoryHasOne()
    {
        var posted = Post("Same", StatusSeverity.Info);
        var later = _reducer.Reduce(posted, new Tick(_start.AddSeconds(3))).State;

        var again = _reducer.Reduce(later with { Message = "Same" }, new Tick(_start.AddSeconds(4))).State;
        var expired = _reducer.Reduce(again, new Tick(_start.AddSeconds(5))).State;

        Assert.Single(again.StatusHistory);
        Assert.Equal(_start, again.Status!.PostedAt);
        Assert.Null(expired.Status);
    }

    [Fact]
    public void GivenANewText_WhenPosted_ThenItReplacesTheOldOneAndBothAreInTheHistoryNewestFirst()
    {
        var first = Post("One", StatusSeverity.Info);

        var second = _reducer.Reduce(first with { Message = "Two" }, new Tick(_start.AddSeconds(1))).State;

        Assert.Equal("Two", second.Status!.Text);
        Assert.Equal(["Two", "One"], second.StatusHistory.Select(message => message.Text));
    }

    [Fact]
    public void GivenMoreThanFiftyMessages_WhenPosting_ThenTheHistoryKeepsTheNewestFifty()
    {
        var state = AppState.Initial;

        for (var index = 0; index < 60; index++)
        {
            state = _reducer.Reduce(state with { Message = $"Message {index}" }, new Tick(_start)).State;
        }

        Assert.Equal(StatusMessages.HistoryLimit, state.StatusHistory.Count);
        Assert.Equal("Message 59", state.StatusHistory[0].Text);
        Assert.Equal("Message 10", state.StatusHistory[^1].Text);
    }

    [Fact]
    public void GivenAShowingMessage_WhenEscIsPressedInNormalMode_ThenItEndsAndStaysInTheHistory()
    {
        var posted = Post("Notice", StatusSeverity.Warning);

        var escaped = _reducer.Reduce(posted, new KeyPressed("Esc", _start.AddSeconds(1))).State;

        Assert.Null(escaped.Status);
        Assert.Null(escaped.Message);
        Assert.Single(escaped.StatusHistory);
    }

    [Fact]
    public void GivenAShowingMessage_WhenSpaceXIsPressed_ThenItEnds()
    {
        var posted = Post("Notice", StatusSeverity.Error);

        var first = _reducer.Reduce(posted, new KeyPressed("Space", _start.AddSeconds(1))).State;
        var dismissed = _reducer.Reduce(first, new KeyPressed("x", _start.AddSeconds(1))).State;

        Assert.Null(dismissed.Status);
        Assert.Equal("", dismissed.PendingKeys);
    }

    [Fact]
    public void GivenMessages_WhenPressingSpaceN_ThenTheOverlayOpensScrollsAndEscCloses()
    {
        var state = Post("A", StatusSeverity.Info);
        state = _reducer.Reduce(state with { Message = "B" }, new Tick(_start)).State;
        state = _reducer.Reduce(state with { Message = "C" }, new Tick(_start)).State;

        var pending = _reducer.Reduce(state, new KeyPressed("Space", _start)).State;
        var opened = _reducer.Reduce(pending, new KeyPressed("n", _start)).State;
        var scrolled = _reducer.Reduce(opened, new KeyPressed("j", _start)).State;
        var past = _reducer.Reduce(_reducer.Reduce(scrolled, new KeyPressed("j", _start)).State, new KeyPressed("j", _start)).State;
        var closed = _reducer.Reduce(past, new KeyPressed("Esc", _start)).State;

        Assert.Equal(InputMode.Help, opened.Mode);
        Assert.True(opened.MessagesOpen);
        Assert.Equal(1, scrolled.HelpScroll);
        Assert.Equal(2, past.HelpScroll);
        Assert.Equal(InputMode.Normal, closed.Mode);
        Assert.False(closed.MessagesOpen);
    }

    [Fact]
    public void GivenAnErrorFromAProfileFailure_WhenReduced_ThenItIsPostedAsAnError()
    {
        var failed = _reducer.Reduce(AppState.Initial, new ProfileOperationFailed("The secret store failed")).State;

        Assert.Equal(StatusSeverity.Error, failed.Status!.Severity);
        Assert.Equal("Error: The secret store failed", failed.Status.Line);
    }

    [Fact]
    public void GivenAMessageSetBeforeTheFirstReduce_WhenTheFirstTickArrives_ThenItIsStampedFromThatTick()
    {
        var shown = _reducer.Reduce(AppState.Initial with { Message = "Early" }, new Tick(_start)).State;

        Assert.Equal(_start, shown.Status!.PostedAt);
    }

    [Fact]
    public void GivenAShowingMessage_WhenAKeyPressCarriesTheTime_ThenItExpiresWithoutATick()
    {
        var posted = Post("Notice", StatusSeverity.Info);

        var expired = _reducer.Reduce(posted, new KeyPressed("j", _start.AddSeconds(6))).State;

        Assert.Null(expired.Status);
    }

    [Fact]
    public void GivenAShowingMessage_WhenAnActionRequestCarriesTheTime_ThenItExpiresWithoutATick()
    {
        var posted = Post("Notice", StatusSeverity.Info);

        var expired = _reducer.Reduce(posted, new ActionRequested(ActionIds.NavigateDown, 1, _start.AddSeconds(6))).State;

        Assert.Null(expired.Status);
    }

    [Fact]
    public void GivenARankPickup_WhenEscIsPressedWithAMessageShowing_ThenTheMessageStaysForThePickupToClaimEsc()
    {
        var posted = Post("Notice", StatusSeverity.Warning);
        var tab = new TabId(1);
        var withPickup = posted with
        {
            Session = SiteSession.Empty(null) with { Tabs = [TabState.Opened(tab, new ViewDefinition(ViewSourceKind.Jql, "t", ViewLayout.List, "t"), 1, 1, 10) with { RankPickup = "A-1" }], Active = tab },
        };

        var escaped = _reducer.Reduce(withPickup, new KeyPressed("Esc", _start.AddSeconds(1))).State;

        Assert.Equal("Notice", escaped.Status!.Text);
    }

    [Fact]
    public void GivenAStartupMessageWithoutAPost_WhenNothingIsPosted_ThenTheSeverityResetsToInfo()
    {
        var state = AppState.Initial with { MessageSeverity = StatusSeverity.Warning };

        var settled = StatusMessages.Settle(state, null);

        Assert.Equal(StatusSeverity.Info, settled.MessageSeverity);
    }

    [Fact]
    public void GivenAnErrorAtItsLifetime_WhenTheSameTextIsPostedAgain_ThenItShowsAgainAndIsRecorded()
    {
        var posted = Post("Failed", StatusSeverity.Error);
        var repeat = StatusMessages.Fail(posted, "Failed");

        var settled = _reducer.Reduce(repeat, new Tick(_start.AddSeconds(30))).State;

        Assert.Equal("Failed", settled.Status!.Text);
        Assert.Equal(_start.AddSeconds(30), settled.Status.PostedAt);
        Assert.Equal(2, settled.StatusHistory.Count);
    }

    [Fact]
    public void GivenAnErrorBeforeItsLifetime_WhenTheSameTextIsPostedAgain_ThenItIsStillIgnored()
    {
        var posted = Post("Failed", StatusSeverity.Error);
        var repeat = StatusMessages.Fail(posted, "Failed");

        var settled = _reducer.Reduce(repeat, new Tick(_start.AddSeconds(29))).State;

        Assert.Equal(_start, settled.Status!.PostedAt);
        Assert.Single(settled.StatusHistory);
    }

    [Fact]
    public void GivenAMessageOverAnOpenedIssue_WhenEscIsPressedTwice_ThenTheMessageEndsFirstAndThenTheIssueCloses()
    {
        var state = WithOpenedIssue();
        var posted = _reducer.Reduce(state with { Message = "Notice" }, new Tick(_start)).State;

        var first = _reducer.Reduce(posted, new KeyPressed("Esc", _start.AddSeconds(1))).State;
        var second = _reducer.Reduce(first, new KeyPressed("Esc", _start.AddSeconds(2))).State;

        Assert.Null(first.Status);
        Assert.Single(first.Session.Tabs[0].Detail);
        Assert.Empty(second.Session.Tabs[0].Detail);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GivenKeymapProblems_WhenReloadingTwiceWithTheSameResult_ThenTheHistoryHoldsEachEntryOnce(int count)
    {
        string[] problems = count == 1 ? ["a"] : ["a", "b"];
        var message = count == 1 ? "keymap.json: a" : "keymap.json: a (and 1 more; see the Messages overlay)";
        var reloaded = new KeymapReloaded(message, problems);

        var first = _reducer.Reduce(AppState.Initial, reloaded).State;
        var second = _reducer.Reduce(first, reloaded).State;

        Assert.Equal(
            first.StatusHistory.Select(entry => entry.Text).Order(),
            second.StatusHistory.Select(entry => entry.Text).Order());
        Assert.Equal(count == 1 ? 1 : 3, second.StatusHistory.Count);
    }

    [Fact]
    public void GivenKeymapProblemsInTheHistory_WhenReloadingCleanly_ThenTheOldProblemsAreGone()
    {
        var bad = _reducer.Reduce(AppState.Initial, new KeymapReloaded("keymap.json: a", ["a", "b"])).State;

        var clean = _reducer.Reduce(bad, new KeymapReloaded("Keymap reloaded")).State;

        Assert.DoesNotContain(clean.StatusHistory, message => message.Text == "keymap.json: b");
    }

    [Fact]
    public void GivenSeveralKeymapProblemsAtStartup_WhenReporting_ThenTheNoticeJoinsAndEachProblemIsAWarning()
    {
        var load = new KeymapLoadResult([], ["a", "b"]);
        var start = AppState.Initial with { Message = "Settings problem" };

        var state = StatusMessages.KeymapStartup(start, load);

        Assert.Equal("Settings problem | keymap.json: a (and 1 more; see the Messages overlay)", state.Message);
        Assert.Equal(StatusSeverity.Warning, state.MessageSeverity);
        Assert.Equal(["keymap.json: a", "keymap.json: b"], [.. state.StatusHistory.Select(message => message.Text).Order()]);
    }

    [Fact]
    public void GivenASingleKeymapProblemAtStartup_WhenReporting_ThenOnlyTheNoticeShows()
    {
        var state = StatusMessages.KeymapStartup(AppState.Initial, new KeymapLoadResult([], ["a"]));

        Assert.Equal("keymap.json: a", state.Message);
        Assert.Empty(state.StatusHistory);
    }

    [Fact]
    public void GivenNoKeymapLoadAtStartup_WhenReporting_ThenTheMessageIsUntouchedOrCleared()
    {
        Assert.Null(StatusMessages.KeymapStartup(AppState.Initial, null).Message);
        var kept = StatusMessages.KeymapStartup(AppState.Initial with { Message = "x" }, new KeymapLoadResult([], []));

        Assert.Equal("x", kept.Message);
    }

    private AppState WithOpenedIssue()
    {
        var profiles = ProfileCatalog.Empty.With(new Profile("work", "https://work.atlassian.net", "me@work.test"));
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var sized = _reducer.Reduce(start, new ViewportChanged(12, 80)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0];
        var status = new IssueStatus("1", "To Do", "new");
        var type = new IssueTypeSummary("10", "Story", 0, false);
        var row = new IssueSummary("1", "A-1", "Issue 1", status, type, null, null, [], null, null, "A");
        var page = new IssuePage([row], null, null);
        var loaded = _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _start)).State;

        return _reducer.Reduce(loaded, new KeyPressed("Enter", _start)).State;
    }

    private AppState Post(string text, StatusSeverity severity)
    {
        var state = AppState.Initial with { Message = text, MessageSeverity = severity };

        return _reducer.Reduce(state, new Tick(_start)).State;
    }
}
