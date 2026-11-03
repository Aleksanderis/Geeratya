using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.State;

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

    private AppState Post(string text, StatusSeverity severity)
    {
        var state = AppState.Initial with { Message = text, MessageSeverity = severity };

        return _reducer.Reduce(state, new Tick(_start)).State;
    }
}
