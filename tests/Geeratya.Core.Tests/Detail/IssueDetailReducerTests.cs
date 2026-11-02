using System.Globalization;
using Geeratya.Core.Cache;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.RemoteLinks.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Detail;

[Trait("Os", "Independent")]
public class IssueDetailReducerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _status = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _type = new("10", "Task", 0, false);
    private static readonly IssueTypeSummary _epic = new("11", "Epic", 1, false);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenListWithSelection_WhenPressingEnter_ThenTheSelectedIssueOpensAndAllThreePartsLoad()
    {
        var state = Move(ListState(), 1);

        var result = Press(state, "Enter");

        var tab = Active(result.State);
        var detail = Assert.Single(tab.Detail);
        var issue = Assert.Single(result.Effects.OfType<LoadIssueDetailEffect>());
        var comments = Assert.Single(result.Effects.OfType<LoadIssueCommentsEffect>());
        var links = Assert.Single(result.Effects.OfType<LoadRemoteLinksEffect>());
        Assert.Equal("A-2", detail.Key);
        Assert.Equal("A-2", issue.Key);
        Assert.Equal(0, comments.StartAt);
        Assert.Equal(detail.IssueRequest, issue.RequestId);
        Assert.Equal(detail.CommentsRequest, comments.RequestId);
        Assert.Equal(detail.LinksRequest, links.RequestId);
        Assert.Equal(3, new[] { issue.RequestId, comments.RequestId, links.RequestId }.Distinct().Count());
        Assert.All(new[] { issue.Tab, comments.Tab, links.Tab }, id => Assert.Equal(tab.Id, id));
        Assert.Equal(1, tab.Selected);
    }

    [Fact]
    public void GivenCachedIssue_WhenItArrivesWhileLoading_ThenItShowsAsStaleAndTheLoadResultReplacesIt()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];
        var savedAt = _now.AddHours(-5);
        var old = Detailed("A-1") with { Summary = "Old summary" };

        var cached = Deliver(opened.State, new IssueDetailCached(tab.Id, detail.IssueRequest, new CachedIssue(old, savedAt)));

        var shown = Active(cached).Detail[0];
        Assert.True(shown.IssueStale);
        Assert.IsType<Loading>(shown.IssueLoad);
        Assert.Equal(detail.IssueRequest, shown.IssueRequest);
        Assert.Equal(savedAt, shown.LoadedAt);
        Assert.Contains("Old summary", Text(shown));
        Assert.Equal(NetworkStatusKind.Stale, TabStatus.Of(Active(cached), cached.Session.Link, _now).Kind);

        var fresh = Deliver(cached, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1") with { Summary = "New summary" }, _now));

        var replaced = Active(fresh).Detail[0];
        Assert.False(replaced.IssueStale);
        Assert.Equal(LoadState.Idle, replaced.IssueLoad);
        Assert.Contains("New summary", Text(replaced));
        Assert.DoesNotContain("Old summary", Text(replaced));
        Assert.NotEqual(NetworkStatusKind.Stale, TabStatus.Of(Active(fresh), fresh.Session.Link, _now).Kind);
    }

    [Fact]
    public void GivenIssueAlreadyLoaded_WhenALateCacheReplyArrives_ThenItIsDropped()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];
        var loaded = Deliver(opened.State, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1"), _now));

        var late = Deliver(loaded, new IssueDetailCached(tab.Id, detail.IssueRequest, new CachedIssue(Detailed("A-1") with { Summary = "Old" }, _now.AddDays(-1))));
        var wrongId = Deliver(opened.State, new IssueDetailCached(tab.Id, detail.IssueRequest + 100, new CachedIssue(Detailed("A-1"), _now)));

        Assert.Equal(loaded, late);
        Assert.Equal(opened.State, wrongId);
    }

    [Fact]
    public void GivenCachedIssueShown_WhenTheLoadFails_ThenTheCachedIssueStaysWithTheErrorOnTheLoadState()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];
        var cached = Deliver(opened.State, new IssueDetailCached(tab.Id, detail.IssueRequest, new CachedIssue(Detailed("A-1"), _now)));

        var failed = Deliver(cached, new IssueDetailFailed(tab.Id, detail.IssueRequest, new JiraError(JiraErrorKind.Network, null, "down")));

        var shown = Active(failed).Detail[0];
        Assert.NotNull(shown.Issue);
        Assert.IsType<Failed>(shown.IssueLoad);
    }

    [Fact]
    public void GivenIssueLoaded_WhenApplied_ThenItIsWrittenToTheCacheOnce()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];

        var result = _reducer.Reduce(opened.State, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1"), _now));
        var stale = _reducer.Reduce(opened.State, new IssueDetailLoaded(tab.Id, detail.IssueRequest + 100, Detailed("A-1"), _now));

        var write = Assert.Single(result.Effects.OfType<WriteIssueCacheEffect>());
        Assert.Equal("A-1", write.Issue.Detail.Key);
        Assert.Equal(_now, write.Issue.SavedAt);
        Assert.Empty(stale.Effects.OfType<WriteIssueCacheEffect>());
    }

    [Fact]
    public void GivenNoRows_WhenPressingEnter_ThenNothingOpens()
    {
        var state = Start();

        var result = Press(state, "Enter");

        Assert.Equal(state, result.State);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenOpenIssue_WhenAllPartsArrive_ThenTheDocumentHasHeaderDescriptionAndComments()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];

        var state = Deliver(opened.State, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1"), _now));

        state = Deliver(state, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, CommentPage(2), _now));
        state = Deliver(state, new IssueRemoteLinksLoaded(tab.Id, detail.LinksRequest, [], _now));

        var text = Text(Active(state).Detail[0]);
        Assert.Contains("A-1 · Task", text);
        Assert.Contains("Status    To Do", text);
        Assert.Contains("Reporter  Rita Reporter", text);
        Assert.Contains("Description", text);
        Assert.Contains("The description of A-1", text);
        Assert.Contains("Comments (2)", text);
        Assert.Contains("Writer 2 · 2 h ago", text);
        Assert.Contains("Comment body 2", text);
        Assert.Equal(LoadState.Idle, Active(state).Detail[0].IssueLoad);
        Assert.Empty(Active(state).Detail[0].RemoteLinks!);
    }

    [Fact]
    public void GivenOpenIssue_WhenOnlyTheRowIsKnown_ThenTheHeaderShowsTheRowAndLoadingMarks()
    {
        var opened = Press(ListState(), "Enter");

        var text = Text(Active(opened.State).Detail[0]);

        Assert.Contains("A-1 · Task", text);
        Assert.Contains("Issue 1", text);
        Assert.Contains("Loading…", text);
    }

    [Fact]
    public void GivenIssueWithoutDescriptionOrComments_WhenLoaded_ThenTheEmptyStatesAreSaid()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];
        var state = Deliver(
            opened.State,
            new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1") with { Description = null }, _now));

        state = Deliver(state, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, CommentPage(0), _now));

        var text = Text(Active(state).Detail[0]);
        Assert.Contains("No description.", text);
        Assert.Contains("No comments.", text);
    }

    [Fact]
    public void GivenCommentsFail_WhenTheIssueLoaded_ThenTheIssueShowsWithARetryLineAndEnterRetries()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];
        var state = Deliver(opened.State, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1"), _now));

        state = Deliver(
            state,
            new IssueDetailFailed(tab.Id, detail.CommentsRequest, new JiraError(JiraErrorKind.Timeout, null, "t")));

        var failed = Active(state).Detail[0];
        Assert.Contains("The description of A-1", Text(failed));
        Assert.Contains("Jira did not answer in time. (Enter to retry)", Text(failed));

        var retry = Press(state, "Enter");
        var effect = Assert.Single(retry.Effects.OfType<LoadIssueCommentsEffect>());
        Assert.Equal(0, effect.StartAt);
        Assert.NotEqual(detail.CommentsRequest, effect.RequestId);
        Assert.Equal(effect.RequestId, Active(retry.State).Detail[0].CommentsRequest);
    }

    [Theory]
    [InlineData(JiraErrorKind.NotFound)]
    [InlineData(JiraErrorKind.Forbidden)]
    public void GivenIssueIsMissingOrHidden_WhenItFails_ThenTheSameNotFoundTextShowsWithTheListUnderneath(
        JiraErrorKind kind)
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];

        var state = Deliver(
            opened.State,
            new IssueDetailFailed(tab.Id, detail.IssueRequest, new JiraError(kind, null, "no")));

        var text = Text(Active(state).Detail[0]);
        Assert.Contains("Not found or no access: A-1", text);
        Assert.DoesNotContain("Comments", text);
        Assert.Equal(3, Active(state).Rows.Count);
    }

    [Fact]
    public void GivenNetworkFailure_WhenTheIssueFails_ThenTheMessageShowsAndEscReturnsToTheList()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var failed = Deliver(
            opened.State,
            new IssueDetailFailed(
                tab.Id,
                tab.Detail[0].IssueRequest,
                new JiraError(JiraErrorKind.Network, null, "x")));

        Assert.Contains("Cannot reach Jira.", Text(Active(failed).Detail[0]));

        var back = Press(failed, "Esc");

        Assert.Empty(Active(back.State).Detail);
        Assert.Equal(3, Active(back.State).Rows.Count);
    }

    [Fact]
    public void GivenStaleReplies_WhenTheyArrive_ThenTheyChangeNothing()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];

        var wrongId = Deliver(opened.State, new IssueDetailLoaded(tab.Id, detail.IssueRequest + 100, Detailed("A-1"), _now));
        var wrongTab = Deliver(opened.State, new IssueDetailLoaded(new TabId(9999), detail.IssueRequest, Detailed("A-1"), _now));
        var applied = Deliver(opened.State, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1"), _now));
        var repeated = Deliver(applied, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1") with { Summary = "again" }, _now));

        Assert.Equal(opened.State, wrongId);
        Assert.Equal(opened.State, wrongTab);
        Assert.Equal("Issue 1", Active(repeated).Detail[0].Issue!.Summary);
    }

    [Fact]
    public void GivenIssuePopped_WhenItsReplyArrives_ThenItIsIgnored()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];
        var back = Press(opened.State, "Esc").State;

        var late = Deliver(back, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1"), _now));

        Assert.Equal(back, late);
        Assert.Empty(Active(late).Detail);
    }

    [Fact]
    public void GivenOpenIssue_WhenPressingEscAndEscAgain_ThenTheListReturnsWithSelectionAndEscAtTheViewDoesNothing()
    {
        var state = Move(ListState(), 2);
        var opened = Press(state, "Enter").State;

        var back = Press(opened, "Esc");
        var again = Press(back.State, "Esc");

        var tab = Active(back.State);
        Assert.Empty(tab.Detail);
        Assert.Equal(2, tab.Selected);
        Assert.Equal("A-3", tab.SelectedKey);
        Assert.Equal(back.State, again.State);
    }

    [Fact]
    public void GivenOpenIssueAndAStatusNotice_WhenPressingEsc_ThenTheNoticeClearsFirstAndTheIssueStays()
    {
        var opened = Press(ListState(), "Enter").State with { Message = "Something happened." };

        var first = Press(opened, "Esc");
        var second = Press(first.State, "Esc");

        Assert.Null(first.State.Message);
        Assert.Single(Active(first.State).Detail);
        Assert.Empty(Active(second.State).Detail);
    }

    [Fact]
    public void GivenDetailOnAStack_WhenOpeningAnotherByKey_ThenBothStayAndEscPopsOneLevel()
    {
        var opened = Press(ListState(), "Enter").State;
        var tab = Active(opened);

        var pushed = _reducer.Reduce(opened, new IssueDetailOpenRequested(tab.Id, "A-9", null));
        var popped = Press(pushed.State, "Esc");

        Assert.Equal(["A-1", "A-9"], Active(pushed.State).Detail.Select(detail => detail.Key));
        Assert.Equal(["A-1"], Active(popped.State).Detail.Select(detail => detail.Key));
    }

    [Fact]
    public void GivenFullStack_WhenOpeningAnother_ThenTheStackStaysAtItsBound()
    {
        var state = Press(ListState(), "Enter").State;
        var tab = Active(state);

        for (var index = 0; index < DetailPaging.MaxStack + 5; index++)
        {
            state = _reducer.Reduce(state, new IssueDetailOpenRequested(tab.Id, $"A-{index + 10}", null)).State;
        }

        Assert.Equal(DetailPaging.MaxStack, Active(state).Detail.Count);
    }

    [Fact]
    public void GivenLongIssue_WhenMovingTheCursor_ThenItMovesByLineAndTheWindowFollowsInRange()
    {
        var state = LoadedLongIssue(commentCount: 20, rows: 10);
        var document = Active(state).Detail[0].Document!;
        var lines = document.Lines.Count;
        var viewport = DetailLayout.ViewportRows(10);

        var down = Press(state, "j").State;
        var page = Press(down, "Ctrl-d").State;
        var bottom = Press(page, "G").State;
        var beyond = Press(bottom, "j").State;
        var top = Press(Press(beyond, "g").State, "g").State;
        var above = Press(top, "k").State;

        Assert.Equal(document.Stops[0], Active(state).Detail[0].Cursor);
        Assert.Equal(document.Stops[1], Active(down).Detail[0].Cursor);
        Assert.Equal(0, Active(down).Detail[0].Scroll);
        Assert.InRange(Active(page).Detail[0].Cursor, document.Stops[1] + (viewport / 2), lines);
        Assert.Equal(document.Stops[^1], Active(bottom).Detail[0].Cursor);
        Assert.Equal(lines - viewport, Active(bottom).Detail[0].Scroll);
        Assert.Equal(document.Stops[^1], Active(beyond).Detail[0].Cursor);
        Assert.Equal(lines - viewport, Active(beyond).Detail[0].Scroll);
        Assert.Equal(document.Stops[0], Active(top).Detail[0].Cursor);
        Assert.Equal(0, Active(top).Detail[0].Scroll);
        Assert.Equal(document.Stops[0], Active(above).Detail[0].Cursor);
    }

    [Fact]
    public void GivenLongIssue_WhenMovingTheCursor_ThenItNeverRestsOnAPanelEdge()
    {
        var state = LoadedLongIssue(commentCount: 20, rows: 10);
        var document = Active(state).Detail[0].Document!;

        for (var step = 0; step < document.Lines.Count; step++)
        {
            state = Press(state, "j").State;

            Assert.Contains(Active(state).Detail[0].Cursor, document.Stops);
            Assert.DoesNotMatch("^[╭╰]", document.Lines[Active(state).Detail[0].Cursor].PlainText);
        }
    }

    [Fact]
    public void GivenCursorAtTheWindowEdge_WhenMovingBack_ThenTheWindowScrollsOnlyWhenTheCursorLeavesIt()
    {
        var state = LoadedLongIssue(commentCount: 20, rows: 10);
        var viewport = DetailLayout.ViewportRows(10);
        var edge = state;

        while (Active(edge).Detail[0].Cursor < viewport + 2)
        {
            edge = Press(edge, "j").State;
        }

        var scrolled = Active(edge).Detail[0];
        var inside = Active(Press(edge, "k").State).Detail[0];

        Assert.True(scrolled.Scroll > 0);
        Assert.Equal(scrolled.Scroll, inside.Scroll);
        Assert.True(inside.Cursor < scrolled.Cursor);
    }

    [Fact]
    public void GivenScrolledToTheBottom_WhenTheTerminalGrowsOrNarrows_ThenTheDocumentIsRelaidOutAndScrollClamped()
    {
        var state = LoadedLongIssue(commentCount: 20, rows: 10);
        var bottom = Press(state, "G").State;

        var taller = _reducer.Reduce(bottom, new ViewportChanged(200, 80)).State;
        var narrower = _reducer.Reduce(bottom, new ViewportChanged(10, 40)).State;

        var before = Active(bottom).Detail[0];
        var tallerDetail = Active(taller).Detail[0];
        var narrowDetail = Active(narrower).Detail[0];
        Assert.Equal(0, tallerDetail.Scroll);
        Assert.Equal(40, narrowDetail.Document!.Width);
        Assert.True(narrowDetail.Document.Lines.Count > before.Document!.Lines.Count);
        Assert.All(narrowDetail.Document.Lines, line => Assert.True(line.PlainText.Length <= 40));
        Assert.InRange(
            narrowDetail.Scroll,
            before.Scroll,
            narrowDetail.Document.Lines.Count - DetailLayout.ViewportRows(10));
    }

    [Fact]
    public void GivenMoreCommentsThanOnePage_WhenPressingEnter_ThenTheNextPageLoadsAndAppendsOnce()
    {
        var state = LoadedLongIssue(commentCount: 3, rows: 10, hasMore: true);
        var detail = Active(state).Detail[0];
        Assert.Contains("Load more comments (Enter)", Text(detail));
        Assert.Equal(3, detail.NextComments);

        var requested = Press(state, "Enter");
        var effect = Assert.Single(requested.Effects.OfType<LoadIssueCommentsEffect>());
        var again = Press(requested.State, "Enter");
        var arrived = Deliver(
            requested.State,
            new IssueCommentsLoaded(Active(state).Id, effect.RequestId, CommentPage(2, startAt: 3, total: 5), _now));

        Assert.Equal(3, effect.StartAt);
        Assert.Contains("Loading more comments…", Text(Active(requested.State).Detail[0]));
        Assert.Empty(again.Effects);
        var loaded = Active(arrived).Detail[0];
        Assert.Equal(5, loaded.Comments.Count);
        Assert.Contains("Comments (5)", Text(loaded));
        Assert.DoesNotContain("Load more comments", Text(loaded));
    }

    [Fact]
    public void GivenMoreCommentsThanTheCap_WhenPagesArrive_ThenTheListStopsAtTheCapWithoutALoadMoreRow()
    {
        var state = LoadedLongIssue(commentCount: 0, rows: 10);
        var tab = Active(state);

        var big = Active(state).Detail[0] with
        {
            Comments = CommentPage(DetailPaging.MaxComments - 1).Items,
            NextComments = DetailPaging.MaxComments - 1,
            CommentsRequest = 77,
            CommentsLoad = new Loading(LoadReason.NextPage),
        };
        state = Replace(state, big);

        state = Deliver(
            state,
            new IssueCommentsLoaded(tab.Id, 77, CommentPage(5, startAt: DetailPaging.MaxComments - 1, total: 5000, last: false), _now));

        var capped = Active(state).Detail[0];
        Assert.Equal(DetailPaging.MaxComments, capped.Comments.Count);
        Assert.Null(capped.NextComments);
        Assert.DoesNotContain("Load more comments", Text(capped));
        Assert.Contains("Showing the first comments only.", Text(capped));
    }

    [Fact]
    public void GivenOpenIssue_WhenTheTabIsClosed_ThenTheStackAndItsLateRepliesAreGone()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var closed = _reducer.Reduce(opened.State, new TabClosed(tab.Id));

        var late = Deliver(
            closed.State,
            new IssueDetailLoaded(tab.Id, tab.Detail[0].IssueRequest, Detailed("A-1"), _now));

        Assert.Empty(closed.State.Session.Tabs);
        Assert.Contains(closed.Effects, effect => effect is CancelTabEffect cancel && cancel.Tab == tab.Id);
        Assert.Equal(closed.State, late);
    }

    [Fact]
    public void GivenRemoteLinkFailure_WhenIssueAndCommentsLoaded_ThenTheBreadcrumbSignalsItAndTextStillShows()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];
        var state = Deliver(opened.State, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1"), _now));

        state = Deliver(
            state,
            new IssueDetailFailed(tab.Id, detail.LinksRequest, new JiraError(JiraErrorKind.Forbidden, null, "x")));

        var links = Active(state).Detail[0];
        Assert.IsType<Failed>(links.LinksLoad);
        Assert.Null(links.RemoteLinks);
        Assert.Contains("The description of A-1", Text(links));
    }

    [Fact]
    public void GivenEpicArrives_WhenTheIssueLoads_ThenItsChildrenAreRequestedOnceWithAFreshRequestId()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];

        var result = _reducer.Reduce(
            opened.State,
            new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1", _epic), _now));

        var children = Assert.Single(result.Effects.OfType<LoadIssueChildrenEffect>());
        var loaded = Active(result.State).Detail[0];
        Assert.Equal("A-1", children.Key);
        Assert.Equal(tab.Id, children.Tab);
        Assert.Equal(loaded.ChildrenRequest, children.RequestId);
        Assert.NotEqual(0, children.RequestId);
        Assert.DoesNotContain(
            children.RequestId,
            new[] { detail.IssueRequest, detail.CommentsRequest, detail.LinksRequest });
        Assert.IsType<Loading>(loaded.ChildrenLoad);
        Assert.Equal(children.RequestId, result.State.Sequence);
    }

    [Fact]
    public void GivenTaskArrives_WhenTheIssueLoads_ThenNoChildrenAreRequested()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);

        var result = _reducer.Reduce(
            opened.State,
            new IssueDetailLoaded(tab.Id, tab.Detail[0].IssueRequest, Detailed("A-1"), _now));

        Assert.Empty(result.Effects.OfType<LoadIssueChildrenEffect>());
        Assert.Equal(LoadState.Idle, Active(result.State).Detail[0].ChildrenLoad);
        Assert.Null(Active(result.State).Detail[0].Children);
    }

    [Fact]
    public void GivenStaleIssueReply_WhenItArrives_ThenNoChildrenAreRequested()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var stale = tab.Detail[0].IssueRequest + 100;

        var result = _reducer.Reduce(opened.State, new IssueDetailLoaded(tab.Id, stale, Detailed("A-1", _epic), _now));

        Assert.Empty(result.Effects);
        Assert.Equal(opened.State, result.State);
    }

    [Fact]
    public void GivenChildrenRequested_WhenTheyArrive_ThenTheyShowInTheDocumentWithAMoreMarker()
    {
        var state = OpenedEpic(out var tab, out var request);
        var page = new IssuePage([Row(21), Row(22)], new PageCursor("next"), null);

        state = Deliver(state, new IssueChildrenLoaded(tab, request, page, _now));

        var detail = Active(state).Detail[0];
        Assert.Equal(2, detail.Children!.Count);
        Assert.True(detail.ChildrenMore);
        Assert.Equal(LoadState.Idle, detail.ChildrenLoad);
        Assert.Equal(0, detail.ChildrenRequest);
        Assert.Contains("Epic children (2+)", Text(detail));
        Assert.Contains("A-21 Issue 21", Text(detail));
    }

    [Fact]
    public void GivenChildrenReplyForAnotherRequest_WhenItArrives_ThenItIsIgnored()
    {
        var state = OpenedEpic(out var tab, out var request);

        var page = new IssuePage([Row(21)], null, null);

        var after = Deliver(state, new IssueChildrenLoaded(tab, request + 7, page, _now));

        Assert.Equal(state, after);
    }

    [Fact]
    public void GivenChildrenReplyForAPoppedIssue_WhenItArrives_ThenTheStackIsUnchanged()
    {
        var state = OpenedEpic(out var tab, out var request);

        state = Deliver(state, new IssueDetailBackRequested(tab));

        var after = Deliver(state, new IssueChildrenLoaded(tab, request, new IssuePage([Row(21)], null, null), _now));

        Assert.Empty(Active(after).Detail);
        Assert.Equal(state, after);
    }

    [Fact]
    public void GivenChildrenFailure_WhenItArrives_ThenOnlyThatSectionFailsAndTheBreadcrumbSignalsIt()
    {
        var state = OpenedEpic(out var tab, out var request);

        state = Deliver(
            state,
            new IssueDetailFailed(tab, request, new JiraError(JiraErrorKind.Forbidden, null, "x")));

        var detail = Active(state).Detail[0];
        Assert.IsType<Failed>(detail.ChildrenLoad);
        Assert.Null(detail.Children);
        Assert.NotNull(detail.Issue);
        Assert.Contains("The description of A-1", Text(detail));
    }

    [Fact]
    public void GivenRemoteLinksArrive_WhenBuildingTheDocument_ThenTheyShowUnderRemoteLinks()
    {
        var opened = Press(ListState(), "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];
        var link = new RemoteLink("1", "https://example.com/spec", "Design spec", "Confluence");

        var state = Deliver(opened.State, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1"), _now));

        state = Deliver(state, new IssueRemoteLinksLoaded(tab.Id, detail.LinksRequest, [link], _now));

        var text = Text(Active(state).Detail[0]);
        Assert.Contains("Remote links (1)", text);
        Assert.Contains("Design spec · Confluence", text);
        Assert.Contains("https://example.com/spec", text);
    }

    private AppState OpenedEpic(out TabId tab, out long childrenRequest)
    {
        var opened = Press(ListState(), "Enter");
        var active = Active(opened.State);
        var result = _reducer.Reduce(
            opened.State,
            new IssueDetailLoaded(active.Id, active.Detail[0].IssueRequest, Detailed("A-1", _epic), _now));

        tab = active.Id;
        childrenRequest = Assert.Single(result.Effects.OfType<LoadIssueChildrenEffect>()).RequestId;

        return result.State;
    }

    private static AppState Start()
    {
        var profiles = ProfileCatalog.Empty.With(_work);

        return AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static string Text(DetailState detail)
    {
        return string.Join('\n', detail.Document!.Lines.Select(line => line.PlainText));
    }

    private static IssueSummary Row(int number)
    {
        return new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
            $"A-{number}",
            $"Issue {number}",
            _status,
            _type,
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private static IssueDetail Detailed(string key, IssueTypeSummary? type = null)
    {
        var description = AdfParser.Parse(
            $$"""{"version":1,"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"The description of {{key}}"}]}]}""");

        return new IssueDetail(
            "1",
            key,
            "Issue 1",
            _status,
            type ?? _type,
            "High",
            null,
            new IssueAssignee("r", "Rita Reporter"),
            [],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            description,
            null,
            [],
            [],
            []);
    }

    private static OffsetPage<IssueComment> CommentPage(
        int count,
        int startAt = 0,
        int? total = null,
        bool last = true)
    {
        var items = Enumerable.Range(1, count)
            .Select(number => Comment(startAt + number))
            .ToArray();

        return new OffsetPage<IssueComment>(items, startAt, DetailPaging.CommentPageSize, total ?? startAt + count, last);
    }

    private static IssueComment Comment(int number)
    {
        var body = AdfParser.Parse(
            $$"""{"version":1,"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Comment body {{number}}"}]}]}""");

        return new IssueComment(
            $"c{number}",
            new IssueAssignee("w", $"Writer {number}"),
            body,
            _now.AddHours(-2),
            _now.AddHours(-2));
    }

    private static ViewDefinition Definition()
    {
        return new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private AppState Deliver(AppState state, Command command)
    {
        return _reducer.Reduce(state, command).State;
    }

    /// <summary>A tab with three rows, viewport 10 rows by 80 columns.</summary>
    private AppState ListState()
    {
        var state = _reducer.Reduce(Start(), new ViewportChanged(10, 80)).State;
        var opened = _reducer.Reduce(state, new ViewOpenRequested(Definition()));
        var tab = Active(opened.State);
        var page = new IssuePage([Row(1), Row(2), Row(3)], null, null);

        return _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    private AppState Move(AppState state, int index)
    {
        return _reducer.Reduce(state, new SelectionMoved(Active(state).Id, index)).State;
    }

    private AppState LoadedLongIssue(int commentCount, int rows, bool hasMore = false)
    {
        var state = _reducer.Reduce(ListState(), new ViewportChanged(rows, 80)).State;
        var opened = Press(state, "Enter");
        var tab = Active(opened.State);
        var detail = tab.Detail[0];
        var loaded = Deliver(opened.State, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1"), _now));
        var page = CommentPage(commentCount, 0, hasMore ? commentCount + 2 : commentCount, !hasMore);

        return Deliver(loaded, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, page, _now));
    }

    private static AppState Replace(AppState state, DetailState detail)
    {
        var tab = Active(state) with { Detail = [detail] };

        return state with { Session = state.Session with { Tabs = [tab] } };
    }
}
