using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>
/// Long text starts cut short and expands in place; the state is per issue (#678). Panes do not fold (#702).
/// </summary>
[Trait("Os", "Independent")]
public class IssueDetailSectionTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenALongDescription_WhenOpening_ThenItStartsCutWithAMoreLine()
    {
        var state = Opened(LongText(20), []);

        var text = Text(state);

        Assert.Contains("line 1", text);
        Assert.DoesNotContain("line 20", text);
        Assert.Contains(text.Split('\n'), line => line.Contains("more lines   Enter expand"));
    }

    [Fact]
    public void GivenAShortDescription_WhenOpening_ThenNothingIsCutAndNoMoreLineShows()
    {
        var state = Opened(LongText(2), []);

        Assert.DoesNotContain("more line", Text(state));
        Assert.DoesNotContain("Show less", Text(state));
    }

    [Fact]
    public void GivenTheMoreLine_WhenPressingEnterTwice_ThenTheDescriptionExpandsAndCutsShortAgain()
    {
        var state = OnLine(Opened(LongText(20), []), "more lines   Enter expand");

        var expanded = Press(state, "Enter").State;

        Assert.Contains("line 20", Text(expanded));
        Assert.Contains("Show less   Enter collapse", Text(expanded));
        Assert.Equal(["description"], Top(expanded).Expanded);

        var again = Press(OnLine(expanded, "Show less   Enter collapse"), "Enter").State;

        Assert.DoesNotContain("line 20", Text(again));
        Assert.Empty(Top(again).Expanded);
    }

    [Fact]
    public void GivenShowLess_WhenPressingEnter_ThenTheCursorLandsOnTheMoreLineNotInALaterPane()
    {
        var onMore = OnLine(Opened(LongText(30), [Comment("7", 3)]), "more lines   Enter expand");
        var expanded = Press(onMore, "Enter").State;

        var collapsed = Press(OnLine(expanded, "Show less   Enter collapse"), "Enter").State;

        Assert.Contains("more lines   Enter expand", Document(collapsed).Lines[Top(collapsed).Cursor].PlainText);
        Assert.True(Top(collapsed).Cursor < Document(collapsed).Sections[2]);
    }

    [Fact]
    public void GivenAnExpandedDescription_WhenTheIssueReloads_ThenItStaysExpanded()
    {
        var state = Press(OnLine(Opened(LongText(20), []), "more lines   Enter expand"), "Enter").State;
        var reloaded = Deliver(state, new IssueDetailLoaded(Active(state).Id, Top(state).IssueRequest, Issue(LongText(20)), _now));

        Assert.Contains("line 20", Text(reloaded));
    }

    [Fact]
    public void GivenALongComment_WhenOpening_ThenItIsCutAndTheCommentKeysStillFindItOnTheMoreLine()
    {
        var state = Opened(null, [Comment("7", 14)]);
        var onMore = Press(state, "G").State;

        var target = Document(onMore).Targets[Top(onMore).Cursor]!;

        Assert.Equal(DetailTargetKind.Comment, target.Kind);
        Assert.Equal("7", target.Value);
        Assert.True(target.Expands);
        Assert.Contains("more lines   Enter expand", Text(state));
        Assert.DoesNotContain("item 14", Text(state));

        var expanded = Press(onMore, "Enter").State;

        Assert.Contains("item 14", Text(Press(expanded, "G").State));
        Assert.Equal(["comment:7"], Top(expanded).Expanded);
    }

    [Fact]
    public void GivenTheDescription_WhenOpening_ThenItShowsThreeLinesAndTheNoteInFourBodyRows()
    {
        var document = Document(Opened(LongText(20), []));
        var start = document.Sections[document.SectionIds.ToList().IndexOf(PaneIds.Description)];

        Assert.Contains("line 1", document.Lines[start].PlainText);
        Assert.Contains("more lines   Enter expand", document.Lines[start + 3].PlainText);
        Assert.StartsWith("╰", document.Lines[start + 4].PlainText);
    }

    [Fact]
    public void GivenACutDescription_WhenOpening_ThenThePaneShowsTheOneToFourChipAndAScrollbar()
    {
        var document = Document(Opened(LongText(20), []));
        var pane = document.SectionIds.ToList().IndexOf(PaneIds.Description);

        Assert.Equal("1-4 of 39", string.Concat(document.PaneRightTitles[pane].Select(span => span.Text)));
        Assert.NotNull(document.PaneScrolls[pane]);
    }

    [Fact]
    public void GivenADescriptionThatFits_WhenOpening_ThenThePaneHasNoChipAndNoScrollbar()
    {
        var document = Document(Opened(LongText(2), []));
        var pane = document.SectionIds.ToList().IndexOf(PaneIds.Description);

        Assert.Empty(document.PaneRightTitles[pane]);
        Assert.Null(document.PaneScrolls[pane]);
    }

    [Fact]
    public void GivenTheDetail_WhenPressingZaOrSectionKeys_ThenNothingFoldsAndTheCursorStays()
    {
        var state = Opened(LongText(20), []);

        foreach (var keys in new[] { new[] { "z", "a" }, ["]", "]"], ["[", "["] })
        {
            var result = Press(state, keys).State;

            Assert.Equal(Top(state).Cursor, Top(result).Cursor);
            Assert.Equal(Text(state), Text(result));
        }
    }

    [Fact]
    public void GivenCtrlJAndCtrlK_WhenPressed_ThenTheCursorMovesToTheNextAndPreviousPane()
    {
        var state = Opened(LongText(20), []);
        var start = Document(state).SectionAt(Top(state).Cursor);

        var next = Press(state, "Ctrl-j").State;

        Assert.Equal(start + 1, Document(next).SectionAt(Top(next).Cursor));
        Assert.Equal(Document(next).Sections[start + 1], Top(next).Cursor);

        var back = Press(next, "Ctrl-k").State;

        Assert.Equal(start, Document(back).SectionAt(Top(back).Cursor));
    }

    [Fact]
    public void GivenTheLastPane_WhenPressingCtrlJ_ThenTheCursorStays()
    {
        var state = Opened(LongText(2), [Comment("7", 2)]);
        var last = Document(state).SectionIds.Count - 1;

        while (Document(state).SectionAt(Top(state).Cursor) < last)
        {
            state = Press(state, "Ctrl-j").State;
        }

        var after = Press(state, "Ctrl-j").State;

        Assert.Equal(Top(state).Cursor, Top(after).Cursor);
    }

    [Fact]
    public void GivenPaneZoom_WhenPressedInTheDetail_ThenItSaysZoomIsNotThereYetAndNothingZooms()
    {
        var result = Press(Opened(LongText(2), []), "Space", "z");

        Assert.Contains("Zoom is not available", result.State.Message);
        Assert.Null(Active(result.State).Panes.Zoomed);
    }

    private static string LongText(int paragraphs)
    {
        return string.Join("\n\n", Enumerable.Range(1, paragraphs).Select(number => $"line {number}"));
    }

    private static string Text(AppState state)
    {
        return string.Join('\n', Document(state).Lines.Select(line => line.PlainText));
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

    private AppState OnLine(AppState state, string text)
    {
        var steps = 0;

        while (!Document(state).Lines[Top(state).Cursor].PlainText.Contains(text) && steps++ < Document(state).Lines.Count)
        {
            state = Press(state, "j").State;
        }

        Assert.Contains(text, Document(state).Lines[Top(state).Cursor].PlainText);

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

    private AppState Opened(string? description, IReadOnlyList<IssueComment> comments)
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
        var loaded = Deliver(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(description), _now));
        var page = new OffsetPage<IssueComment>(comments, 0, DetailPaging.CommentPageSize, comments.Count, true);

        return Deliver(loaded, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, page, _now));
    }

    private static IssueDetail Issue(string? description = null)
    {
        var document = description is null ? null : AdfMarkdownParser.Parse(description);

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
            document,
            null,
            [],
            [],
            []);
    }

    private static IssueComment Comment(string id, int items)
    {
        var markdown = string.Join('\n', Enumerable.Range(1, items).Select(number => $"- item {number}"));
        var body = AdfMarkdownParser.Parse(markdown);

        return new IssueComment(id, _eve, body, _now.AddHours(-2), _now.AddHours(-2));
    }
}
