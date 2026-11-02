using Geeratya.Core.Suggestions;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Tests.Suggestions;

[Trait("Os", "Independent")]
public class SuggestionPopupTests
{
    private static readonly string[] _none = [];
    private static readonly SuggestionSource _labels = new(SuggestionKind.Labels, null, ["backend", "frontend"]);
    private static readonly SuggestionSource _users = new(SuggestionKind.AssignableUsers, "A-1", []);

    [Fact]
    public void GivenKnownLabels_WhenTheQueryChanges_ThenTheyMatchAtOnceWhileTheLookupWaits()
    {
        var popup = SuggestionPopup.Queried(null, 2, _labels, "back", _none, 7);

        Assert.Equal(SuggestionStatus.Waiting, popup.Status);
        Assert.Equal(7, popup.RequestId);
        Assert.Equal(["backend", "back"], popup.Items.Select(item => item.Label));
    }

    [Fact]
    public void GivenAReplyForAnOldRequest_WhenItArrives_ThenThePopupIgnoresIt()
    {
        var popup = SuggestionPopup.Queried(null, 0, _users, "jo", _none, 8);

        Assert.Null(SuggestionPopup.Loaded(popup, 7, [User("1", "Jo Old")], _none));
        Assert.Null(SuggestionPopup.Slow(popup, 7));
        Assert.Null(SuggestionPopup.Failed(popup, 7, "late"));
    }

    [Fact]
    public void GivenAReplyForUsers_WhenItArrives_ThenJirasOrderStandsAndTheCursorIsOnTheFirst()
    {
        var popup = SuggestionPopup.Queried(null, 0, _users, "jo", _none, 8);

        var loaded = SuggestionPopup.Loaded(popup, 8, [User("2", "Zed (jo@x)"), User("1", "Al")], _none)!;

        Assert.Equal(SuggestionStatus.Loaded, loaded.Status);
        Assert.Equal(["Zed (jo@x)", "Al"], loaded.Items.Select(item => item.Label));
        Assert.Equal("2", loaded.Selected!.Id);
    }

    [Fact]
    public void GivenAChosenUser_WhenTheReplyListsIt_ThenItIsNotOfferedAgain()
    {
        var popup = SuggestionPopup.Queried(null, 0, _users, "jo", _none, 8);

        var loaded = SuggestionPopup.Loaded(popup, 8, [User("2", "Zed"), User("1", "Al")], ["2"])!;

        Assert.Equal(["Al"], loaded.Items.Select(item => item.Label));
    }

    [Fact]
    public void GivenALabelReply_WhenItArrives_ThenItIsMergedWithTheLoadedOnesAndRankedLocally()
    {
        var popup = SuggestionPopup.Queried(null, 0, _labels, "end", _none, 3);

        var loaded = SuggestionPopup.Loaded(popup, 3, [new Suggestion("legend", "legend", null, false)], _none)!;

        Assert.Equal(["backend", "end", "frontend", "legend"], loaded.Items.Select(item => item.Label).Order());
        Assert.Equal(3, loaded.Pool.Count);
    }

    [Fact]
    public void GivenTheCursorOnARow_WhenTheReplyArrives_ThenTheHighlightStaysOnTheSameRow()
    {
        var popup = SuggestionPopup.Queried(null, 0, _labels, "end", _none, 3);
        var moved = SuggestionPopup.Moved(popup, 1);

        var loaded = SuggestionPopup.Loaded(moved, 3, [new Suggestion("aaa-end", "aaa-end", null, false)], _none)!;

        Assert.Equal(moved.Selected!.Id, loaded.Selected!.Id);
    }

    [Fact]
    public void GivenALookupThatIsStillWaiting_WhenItIsSlow_ThenThePopupSaysLoadingOnce()
    {
        var popup = SuggestionPopup.Queried(null, 0, _users, "jo", _none, 8);

        var slow = SuggestionPopup.Slow(popup, 8)!;

        Assert.Equal(SuggestionStatus.Loading, slow.Status);
        Assert.Null(SuggestionPopup.Slow(slow with { Status = SuggestionStatus.Loaded }, 8));
    }

    [Fact]
    public void GivenAFailedLookup_WhenItIsReported_ThenWhatIsLoadedStaysAndTheErrorIsKept()
    {
        var popup = SuggestionPopup.Queried(null, 0, _labels, "back", _none, 4);

        var failed = SuggestionPopup.Failed(popup, 4, "offline")!;

        Assert.Equal(SuggestionStatus.Failed, failed.Status);
        Assert.Equal("offline", failed.Error);
        Assert.Equal(popup.Items, failed.Items);
    }

    [Fact]
    public void GivenTheCursor_WhenMovedPastEitherEnd_ThenItStaysInsideTheList()
    {
        var popup = SuggestionPopup.Queried(null, 0, _labels, "end", _none, 4);

        Assert.Equal(0, SuggestionPopup.Moved(popup, -1).Cursor);
        Assert.Equal(popup.Items.Count - 1, SuggestionPopup.Moved(popup, 99).Cursor);
    }

    [Fact]
    public void GivenAPopupOfTheSameField_WhenTheQueryChanges_ThenItsPoolIsKept()
    {
        var first = SuggestionPopup.Queried(null, 1, _users, "jo", _none, 4);
        var loaded = SuggestionPopup.Loaded(first, 4, [User("1", "Jonas")], _none)!;

        var next = SuggestionPopup.Queried(loaded, 1, _users, "jon", _none, 5);
        var other = SuggestionPopup.Queried(loaded, 2, _users, "jon", _none, 6);

        Assert.Single(next.Pool);
        Assert.Empty(other.Pool);
    }

    private static Suggestion User(string id, string name)
    {
        return new Suggestion(id, name, null, false);
    }
}
