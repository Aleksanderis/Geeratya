using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
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
        Assert.Contains("… 5 more lines   Enter expand", cut[4].Line.PlainText);
        Assert.Equal(9, zoomed.Count);
        Assert.Contains("… 2 more lines   Enter expand", zoomed[7].Line.PlainText);
        Assert.Equal(TextRole.ValueBlock, cut[0].Line.Spans[0].Role);
        Assert.Equal(70, cut[0].Line.Spans.Sum(span => DisplayWidth.Of(span.Text)));
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
        Assert.Contains("newest first", edge);
        Assert.NotNull(document.PaneScrolls[section]);
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

    private static DetailState DetailOf(IReadOnlyList<IssueComment> comments)
    {
        return DetailState.Opened("A-1", null, 1, 2, 3) with { Comments = comments, LoadedAt = _now, CommentsLoad = LoadState.Idle, NextComments = null };
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

    private AppState Opened(IssueComment[] comments, int? next = null, int? total = null)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
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
