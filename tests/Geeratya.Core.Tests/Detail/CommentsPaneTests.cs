using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>The Comments pane (#703): header bars, bodies cut after three lines, expand in place, own scroll.</summary>
[Trait("Os", "Independent")]
public class CommentsPaneTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenALongComment_WhenBuildingRows_ThenThreeLinesAndANoteShowAndSixWhenZoomed()
    {
        var detail = DetailOf([Comment("1", 8)]);

        var cut = CommentRows.Build(detail, 70);
        var zoomed = CommentRows.Build(detail, 70, 6);

        // The header bar, three lines, the note and the gap.
        Assert.Equal(6, cut.Count);
        Assert.Contains("… 5 more lines   za expand", cut[4].Line.PlainText);
        Assert.Equal(9, zoomed.Count);
        Assert.Contains("… 2 more lines   za expand", zoomed[7].Line.PlainText);
        Assert.Equal(TextRole.ValueBlock, cut[0].Line.Spans[0].Role);
        Assert.Equal(70, cut[0].Line.Spans.Sum(span => DisplayWidth.Of(span.Text)));
    }

    [Fact]
    public void GivenAReplyLoadedLast_WhenMovingWithJ_ThenTheCursorVisitsParentReplyThenTheNextComment()
    {
        var reply = Comment("r", 1) with { ParentId = "1" };
        var state = ToComments(Opened([Comment("1", 1), Comment("2", 1), reply]));

        var first = TargetOf(state).Value;
        var second = TargetOf(Press(state, "j").State).Value;
        var third = TargetOf(Press(state, "j", "j").State).Value;

        Assert.Equal(["1", "r", "2"], [first, second, third]);
    }

    [Fact]
    public void GivenAShortComment_WhenBuildingRows_ThenNothingIsCutAndOnlyTheBarIsAStop()
    {
        var rows = CommentRows.Build(DetailOf([Comment("1", 2), Comment("2", 1)]), 70);

        var stops = Enumerable.Range(0, rows.Count).Where(index => rows[index].IsStop).ToArray();

        Assert.DoesNotContain(rows, row => row.Line.PlainText.Contains("more line"));
        Assert.Equal([0, 4], stops);
        Assert.Equal("1", rows[0].Target!.Value);
        Assert.Null(rows[3].Target);
    }

    [Fact]
    public void GivenAnExpandedComment_WhenBuildingRows_ThenEveryBodyLineAndTheShowLessRowAreStops()
    {
        var detail = DetailOf([Comment("1", 8)]) with { Expanded = ["comment:1"] };

        var rows = CommentRows.Build(detail, 70);

        // The bar, eight lines, "Show less", the gap.
        Assert.Equal(11, rows.Count);
        Assert.All(rows.Take(10), row => Assert.True(row.IsStop));
        Assert.True(rows[0].Target!.Collapses);
        Assert.Contains("Show less", rows[9].Line.PlainText);
    }

    [Fact]
    public void GivenTheFirstPageOfALongThread_WhenOpening_ThenTheTitleSaysHowManyAreLoadedAndTheOrder()
    {
        var comments = Enumerable.Range(1, 50).Select(number => Comment(number.ToString(), 1)).ToArray();
        var document = Document(Opened(comments, total: 120));
        var section = document.SectionIds.ToList().IndexOf(PaneIds.Comments);

        var edge = document.Lines[document.PaneStarts[section]].PlainText;

        Assert.Contains("Comments 50 of 120", edge);
        Assert.Contains("oldest first", edge);
        Assert.NotNull(document.PaneScrolls[section]);
    }

    [Fact]
    public void GivenTheNewestFirstSetting_WhenOpeningAnIssue_ThenTheCommentsRunNewestFirst()
    {
        var newest = AppSettings.Default with { CommentsNewestFirst = true };
        var state = Opened([Comment("2", 1), Comment("1", 1)], settings: newest);

        var section = Document(state).SectionIds.ToList().IndexOf(PaneIds.Comments);

        Assert.True(Top(state).CommentsNewestFirst);
        Assert.Contains("newest first", Document(state).Lines[Document(state).PaneStarts[section]].PlainText);
    }

    [Fact]
    public void GivenEveryCommentLoaded_WhenReversing_ThenTheListFlipsInPlaceAndTheChoiceIsSaved()
    {
        var state = Opened([Comment("1", 1), Comment("2", 1), Comment("3", 1)]);

        var reversed = Press(state, "Space", "m", "o");

        var top = Top(reversed.State);
        Assert.Equal(["3", "2", "1"], top.Comments.Select(comment => comment.Id));
        Assert.True(top.CommentsNewestFirst);
        Assert.True(reversed.State.Settings.CommentsNewestFirst);
        Assert.Empty(reversed.Effects.OfType<LoadIssueCommentsEffect>());
        Assert.Single(reversed.Effects.OfType<SaveSettingsEffect>());
        Assert.Contains("newest first", string.Join('\n', Document(reversed.State).Lines.Select(line => line.PlainText)));

        var back = Press(reversed.State, "Space", "m", "o");

        Assert.Equal(["1", "2", "3"], Top(back.State).Comments.Select(comment => comment.Id));
        Assert.False(back.State.Settings.CommentsNewestFirst);
    }

    [Fact]
    public void GivenMoreCommentsUnloaded_WhenReversing_ThenThePagesAreReadAgainNewestFirst()
    {
        var state = Opened([Comment("1", 1), Comment("2", 1)], next: 2, total: 5);

        var reversed = Press(state, "Space", "m", "o");

        var effect = Assert.Single(reversed.Effects.OfType<LoadIssueCommentsEffect>());
        Assert.True(effect.NewestFirst);
        Assert.Equal(0, effect.StartAt);
        Assert.Empty(Top(reversed.State).Comments);
        Assert.Equal(effect.RequestId, Top(reversed.State).CommentsRequest);
    }

    [Fact]
    public void GivenAReplyOfTheOldOrder_WhenItArrivesAfterTheFlip_ThenItIsDropped()
    {
        var state = Opened([Comment("1", 1), Comment("2", 1)], next: 2, total: 5);
        var oldRequest = Top(state).CommentsRequest;

        var reversed = Press(state, "Space", "m", "o").State;
        var stale = new OffsetPage<IssueComment>([Comment("3", 1)], 2, DetailPaging.CommentPageSize, 5, true);
        var late = Deliver(reversed, new IssueCommentsLoaded(Active(reversed).Id, oldRequest, stale, _now));

        Assert.Empty(Top(late).Comments);
        Assert.True(Top(late).CommentsNewestFirst);
    }

    [Fact]
    public void GivenNoIssueOpen_WhenReversing_ThenNothingChanges()
    {
        var reversed = Press(AppState.Initial, "Space", "m", "o");

        Assert.False(reversed.State.Settings.CommentsNewestFirst);
        Assert.Empty(reversed.Effects);
    }

    [Fact]
    public void GivenCutComments_WhenPressingJAndK_ThenTheCursorMovesBetweenCommentsOnly()
    {
        var first = ToComments(Opened([Comment("3", 8), Comment("2", 8), Comment("1", 8)]));

        var second = Press(first, "j").State;
        var back = Press(second, "k").State;

        Assert.Equal("3", TargetOf(first).Value);
        Assert.True(TargetOf(first).Expands);
        Assert.Equal("2", TargetOf(second).Value);
        Assert.Equal("3", TargetOf(back).Value);
    }

    [Fact]
    public void GivenAnExpandedComment_WhenPressingJ_ThenTheCursorMovesByLineAndLeavesPastTheEnd()
    {
        var expanded = Press(ToComments(Opened([Comment("2", 5), Comment("1", 8)])), "Enter").State;

        var lines = new List<string>();
        var walking = expanded;

        for (var step = 0; step < 7; step++)
        {
            walking = Press(walking, "j").State;
            lines.Add(Document(walking).Lines[Top(walking).Cursor].PlainText);
        }

        Assert.Equal(["comment:2"], Top(expanded).Expanded);
        Assert.Contains("item 1", lines[0]);
        Assert.Contains("item 5", lines[4]);
        Assert.Contains("Show less", lines[5]);
        Assert.Equal("1", TargetOf(walking).Value);
        Assert.Equal("2", TargetOf(Press(walking, "k").State).Value);
    }

    [Fact]
    public void GivenAnExpandedComment_WhenCollapsingFromItsLastRow_ThenTheCursorLandsOnItsBar()
    {
        var expanded = Press(ToComments(Opened([Comment("1", 8)])), "Enter", "G").State;

        var cut = Press(expanded, "Enter").State;

        Assert.Empty(Top(cut).Expanded);
        Assert.True(TargetOf(cut).Expands);
        Assert.Contains("Eve Example", Document(cut).Lines[Top(cut).Cursor].PlainText);
    }

    [Fact]
    public void GivenManyComments_WhenMovingDownPastTheWindow_ThenThePaneScrollsAndStaysInsideItsContent()
    {
        var comments = Enumerable.Range(1, 30).Select(number => Comment(number.ToString(), 8)).ToArray();
        var state = ToComments(Opened(comments));

        for (var step = 0; step < 10; step++)
        {
            state = Press(state, "j").State;
        }

        var document = Document(state);
        var scroll = document.PaneScrolls[document.SectionIds.ToList().IndexOf(PaneIds.Comments)]!;
        var rows = CommentRows.Build(Top(state), Panel.InnerWidth(document.Width));

        Assert.Equal("11", TargetOf(state).Value);
        Assert.True(Top(state).CommentsOffset > 0);
        Assert.InRange(scroll.Offset, 0, rows.Count - scroll.Viewport);
        Assert.Contains("Eve Example", document.Lines[Top(state).Cursor].PlainText);
    }

    [Fact]
    public void GivenManyComments_WhenMovingThroughThem_ThenTheRowsAreNotBuiltAgain()
    {
        var comments = Enumerable.Range(1, 30).Select(number => Comment(number.ToString(), 8)).ToArray();
        var state = ToComments(Opened(comments));
        var before = DetailZoom.CommentRowsOf(Top(state), Document(state).Width);

        state = Press(Press(Press(state, "j").State, "j").State, "k").State;

        Assert.Same(before, DetailZoom.CommentRowsOf(Top(state), Document(state).Width));
    }

    [Fact]
    public void GivenBuiltRows_WhenACommentIsExpandedOrTheCommentsChange_ThenTheRowsAreBuiltAgain()
    {
        var detail = DetailOf([Comment("1", 8), Comment("2", 8)]);
        var rows = DetailZoom.CommentRowsOf(detail, 80);

        Assert.Same(rows, DetailZoom.CommentRowsOf(detail with { Cursor = 3 }, 80));

        var expanded = DetailZoom.CommentRowsOf(detail with { Expanded = ["comment:1"] }, 80);
        var refreshed = DetailZoom.CommentRowsOf(detail with { Comments = [Comment("1", 8)] }, 80);
        var narrower = DetailZoom.CommentRowsOf(detail, 60);

        Assert.NotSame(rows, expanded);
        Assert.True(expanded.Count > rows.Count);
        Assert.Contains(refreshed, row => row.IsStop && row.Target is { Value: "1" });
        Assert.DoesNotContain(refreshed, row => row.Target is { Value: "2" });
        Assert.NotSame(rows, narrower);
    }

    [Fact]
    public void GivenCutRows_WhenExpandingAComment_ThenItsBodyLinesAreTheTextShownBefore()
    {
        var detail = DetailOf([Comment("1", 8)]);
        var cut = DetailZoom.CommentRowsOf(detail, 80);

        var expanded = DetailZoom.CommentRowsOf(detail with { Expanded = ["comment:1"] }, 80);

        Assert.NotSame(cut, expanded);
        Assert.Equal(cut[1].Line.PlainText, expanded[1].Line.PlainText);
        Assert.Contains("item 1", expanded[1].Line.PlainText);
    }

    [Fact]
    public void GivenManyComments_WhenPressingG_ThenTheCursorIsOnTheLastCommentAndThePaneEndsThere()
    {
        var comments = Enumerable.Range(1, 30).Select(number => Comment(number.ToString(), 8)).ToArray();

        var state = Press(Opened(comments), "G").State;

        var document = Document(state);
        var scroll = document.PaneScrolls[document.SectionIds.ToList().IndexOf(PaneIds.Comments)]!;

        Assert.Equal("30", TargetOf(state).Value);
        Assert.True(scroll.Offset >= scroll.MaxOffset - 1);
    }

    [Fact]
    public void GivenTheLoadMoreRow_WhenPressingGAndEnter_ThenTheNextPageIsRequested()
    {
        var comments = Enumerable.Range(1, 4).Select(number => Comment(number.ToString(), 1)).ToArray();
        var state = Press(Opened(comments, next: 4, total: 9), "G");

        var requested = Press(state.State, "Enter");

        Assert.Contains("Load more comments", Document(state.State).Lines[Top(state.State).Cursor].PlainText);
        Assert.Single(requested.Effects.OfType<LoadIssueCommentsEffect>());
    }

    [Fact]
    public void GivenTheCursorOnAComment_WhenAnOlderPageArrives_ThenTheCursorStaysOnThatComment()
    {
        var comments = Enumerable.Range(5, 4).Select(number => Comment(number.ToString(), 8)).Reverse().ToArray();
        var state = Press(ToComments(Opened(comments, next: 4, total: 8)), "j", "j").State;
        var tab = Active(state);
        var older = Enumerable.Range(1, 4).Select(number => Comment(number.ToString(), 8)).Reverse().ToArray();
        var page = new OffsetPage<IssueComment>(older, 4, DetailPaging.CommentPageSize, 8, true);
        var loading = Top(state) with { CommentsRequest = 77, CommentsLoad = new Loading(LoadReason.NextPage) };
        var waiting = state with { Session = state.Session with { Tabs = [tab with { Detail = [loading] }] } };

        var arrived = Deliver(waiting, new IssueCommentsLoaded(tab.Id, 77, page, _now));

        Assert.Equal("6", TargetOf(state).Value);
        Assert.Equal("6", TargetOf(arrived).Value);
        Assert.Equal(8, Top(arrived).Comments.Count);
    }

    [Fact]
    public void GivenTheCursorOnAComment_WhenANewerCommentArrivesOnTop_ThenTheCursorStaysOnThatComment()
    {
        var comments = Enumerable.Range(1, 30).Select(number => Comment(number.ToString(), 8)).Reverse().ToArray();
        var state = Press(ToComments(Opened(comments)), "j", "j", "j").State;
        var refreshed = Enumerable.Range(1, 31).Select(number => Comment(number.ToString(), 8)).Reverse().ToArray();

        var arrived = Refresh(state, refreshed);

        Assert.Equal("27", TargetOf(state).Value);
        Assert.Equal("27", TargetOf(arrived).Value);
    }

    [Fact]
    public void GivenTheCursorOnAComment_WhenACommentAboveItIsDeleted_ThenTheCursorStaysOnThatComment()
    {
        var comments = Enumerable.Range(1, 30).Select(number => Comment(number.ToString(), 8)).Reverse().ToArray();
        var state = Press(ToComments(Opened(comments)), "j", "j", "j").State;

        var arrived = Refresh(state, comments[1..]);

        Assert.Equal("27", TargetOf(arrived).Value);
    }

    [Fact]
    public void GivenTheCursorInsideAnExpandedComment_WhenTheListRefreshes_ThenTheCursorKeepsItsLineOfThatComment()
    {
        var comments = Enumerable.Range(1, 10).Select(number => Comment(number.ToString(), 8)).Reverse().ToArray();
        var state = Press(ToComments(Opened(comments)), "j", "Enter", "j", "j", "j").State;
        var line = Document(state).Lines[Top(state).Cursor].PlainText;
        var refreshed = Enumerable.Range(1, 11).Select(number => Comment(number.ToString(), 8)).Reverse().ToArray();

        var arrived = Refresh(state, refreshed);

        Assert.Contains("item 3", line);
        Assert.Equal("9", TargetOf(arrived).Value);
        Assert.Equal(line, Document(arrived).Lines[Top(arrived).Cursor].PlainText);
    }

    private AppState Refresh(AppState state, IssueComment[] comments)
    {
        var tab = Active(state);
        var page = new OffsetPage<IssueComment>(comments, 0, DetailPaging.CommentPageSize, comments.Length, true);
        var loading = Top(state) with { CommentsRequest = 78, CommentsLoad = new Loading(LoadReason.Refresh) };
        var waiting = state with { Session = state.Session with { Tabs = [tab with { Detail = [loading] }] } };

        return Deliver(waiting, new IssueCommentsLoaded(tab.Id, 78, page, _now));
    }

    private static DetailState DetailOf(IReadOnlyList<IssueComment> comments)
    {
        return DetailState.Opened("A-1", null, 1, 2, 3) with
        {
            Comments = comments,
            LoadedAt = _now,
            CommentsLoad = LoadState.Idle,
            NextComments = null,
        };
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static DetailState Top(AppState state)
    {
        return Active(state).Detail[^1];
    }

    private static DetailDocument Document(AppState state)
    {
        return Top(state).Document!;
    }

    private static DetailTarget TargetOf(AppState state)
    {
        return Document(state).Targets[Top(state).Cursor]!;
    }

    private AppState ToComments(AppState state)
    {
        var steps = 0;

        while (Document(state).SectionIds[Document(state).SectionAt(Top(state).Cursor)] != PaneIds.Comments && steps++ < 20)
        {
            state = Press(state, "Ctrl-j").State;
        }

        return state;
    }

    private AppState Deliver(AppState state, Command command)
    {
        return _reducer.Reduce(state, command).State;
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key));
        }

        return result;
    }

    private AppState Opened(
        IssueComment[] comments,
        int? next = null,
        int? total = null,
        AppSettings? settings = null)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active),
            Settings = settings ?? AppSettings.Default,
        };
        var sized = _reducer.Reduce(start, new ViewportChanged(30, 80)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");
        var listed = Deliver(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([row], null, null), _now));
        var shown = Press(listed, "Enter").State;
        var detail = Active(shown).Detail[0];
        var loaded = Deliver(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(), _now));
        var page = new OffsetPage<IssueComment>(
            comments,
            0,
            DetailPaging.CommentPageSize,
            total ?? comments.Length,
            next is null);

        return Deliver(loaded, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, page, _now));
    }

    private static IssueDetail Issue()
    {
        return new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            _story,
            "High",
            _eve,
            _eve,
            [],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            null,
            null,
            [],
            [],
            []);
    }

    private static IssueComment Comment(string id, int items)
    {
        var markdown = string.Join('\n', Enumerable.Range(1, items).Select(number => $"- item {number}"));

        return new IssueComment(id, _eve, AdfMarkdownParser.Parse(markdown), _now.AddHours(-2), _now.AddHours(-2));
    }
}
