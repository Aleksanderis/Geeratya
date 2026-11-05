using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Panes;
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
        Assert.Contains(text.Split('\n'), line => line.Contains("more lines"));
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
        var state = OnLine(Opened(LongText(20), []), "more lines");

        var expanded = Press(state, "z", "a").State;

        Assert.Contains("line 20", Text(expanded));
        Assert.DoesNotContain("Show less", Text(expanded));
        Assert.Contains("za collapse", Text(expanded));
        Assert.Equal(["description"], Top(expanded).Expanded);

        var again = Press(expanded, "z", "a").State;

        Assert.DoesNotContain("line 20", Text(again));
        Assert.Empty(Top(again).Expanded);
    }

    [Fact]
    public void GivenAnExpandedDescription_WhenPressingEnter_ThenTheCursorStaysInTheDescriptionNotInALaterPane()
    {
        var onMore = OnLine(Opened(LongText(30), [Comment("7", 3)]), "more lines");
        var expanded = Press(onMore, "z", "a").State;

        var collapsed = Press(expanded, "z", "a").State;

        Assert.True(Top(collapsed).Cursor < Document(collapsed).Sections[2]);
    }

    [Fact]
    public void GivenAnExpandedDescription_WhenTheIssueReloads_ThenItStaysExpanded()
    {
        var state = Press(OnLine(Opened(LongText(20), []), "more lines"), "z", "a").State;
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
        Assert.Contains("more lines   za expand", Text(state));
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
        Assert.Contains("more lines", document.Lines[start + 3].PlainText);
        Assert.StartsWith("╰", document.Lines[start + 4].PlainText);
    }

    [Fact]
    public void GivenACutDescription_WhenOpening_ThenThePaneShowsTheOneToFourChipAndAScrollbar()
    {
        var document = Document(Opened(LongText(20), []));
        var pane = document.SectionIds.ToList().IndexOf(PaneIds.Description);

        Assert.Equal("1-3 of 39 · za expand", string.Concat(document.PaneRightTitles[pane].Select(span => span.Text)));
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
    public void GivenAPaneScroll_WhenScrollingPastEitherEnd_ThenTheOffsetClampsAndTheChipFollows()
    {
        var scroll = new PaneScroll(0, 39, 3);

        Assert.Equal("1-3 of 39", scroll.Chip());
        Assert.Equal(0, scroll.By(-5).ClampedOffset);
        Assert.Equal("5-7 of 39", scroll.By(4).Chip());
        Assert.Equal(36, scroll.By(500).ClampedOffset);
        Assert.Equal("37-39 of 39", scroll.By(500).Chip());
    }

    [Fact]
    public void GivenACutDescription_WhenPressingJOnItsLastTextRow_ThenItScrollsByALineAndTheChipFollows()
    {
        var state = OnDescriptionRow(Opened(LongText(20), []), 2);
        var before = Top(state).Cursor;

        var scrolled = Press(state, "j").State;
        var pane = Document(scrolled).SectionIds.ToList().IndexOf(PaneIds.Description);
        var start = Document(scrolled).Sections[pane];

        Assert.Equal(1, Top(scrolled).DescriptionOffset);
        Assert.Equal(before, Top(scrolled).Cursor);
        Assert.Equal("2-4 of 39 · za expand", ChipOf(scrolled));
        Assert.Contains("more lines", Document(scrolled).Lines[start + 3].PlainText);
        Assert.DoesNotContain("line 1", Document(scrolled).Lines[start].PlainText);
    }

    [Fact]
    public void GivenAScrolledDescription_WhenPressingKOnItsFirstTextRow_ThenItScrollsBackUpAndThenLeavesAtTheTop()
    {
        var state = OnDescriptionRow(Opened(LongText(20), []), 2);
        state = Press(state, "j", "j").State;
        state = OnDescriptionRow(state, 0);

        var up = Press(state, "k").State;

        Assert.Equal(1, Top(up).DescriptionOffset);

        var top = Press(up, "k").State;

        Assert.Equal(0, Top(top).DescriptionOffset);
        Assert.Equal("1-3 of 39 · za expand", ChipOf(top));

        var left = Press(top, "k").State;

        Assert.Equal(0, Top(left).DescriptionOffset);
        Assert.NotEqual(PaneIds.Description, Document(left).SectionIds[Document(left).SectionAt(Top(left).Cursor)]);
    }

    [Fact]
    public void GivenADescriptionScrolledToItsEnd_WhenPressingJ_ThenItStopsScrollingAndTheCursorMovesOn()
    {
        var scrolled = OnDescriptionRow(Opened(LongText(3), []), 2);

        for (var index = 0; index < 10; index++)
        {
            scrolled = OnDescriptionRow(Press(scrolled, "j").State, 2);
        }

        var pane = Document(scrolled).SectionIds.ToList().IndexOf(PaneIds.Description);
        var scroll = Document(scrolled).PaneScrolls[pane];

        Assert.Equal(scroll!.MaxOffset, Top(scrolled).DescriptionOffset);

        var moved = Press(scrolled, "j").State;

        Assert.Equal(scroll.MaxOffset, Top(moved).DescriptionOffset);
        Assert.Equal(Top(scrolled).Cursor + 1, Top(moved).Cursor);
    }

    [Fact]
    public void GivenAScrolledDescription_WhenPressingEnterOnTheNote_ThenItExpandsInPlace()
    {
        var state = OnDescriptionRow(Opened(LongText(5), []), 2);

        state = Press(state, "j", "j", "j").State;

        var expanded = Press(OnDescriptionRow(state, 3), "z", "a").State;

        Assert.Contains("line 1", Text(expanded));
        Assert.Contains("za collapse", Text(expanded));
    }

    [Fact]
    public void GivenAScrolledDescription_WhenExpandingACommentBelow_ThenTheDescriptionKeepsItsScroll()
    {
        var state = OnDescriptionRow(Opened(LongText(20), [Comment("7", 8)]), 2);
        var scrolled = Press(state, "j", "j").State;

        while (Document(scrolled).SectionIds[Document(scrolled).SectionAt(Top(scrolled).Cursor)] != PaneIds.Comments)
        {
            scrolled = Press(scrolled, "Ctrl-j").State;
        }

        var after = Press(scrolled, "Enter").State;

        Assert.Equal(2, Top(after).DescriptionOffset);
        Assert.Contains("comment:7", Top(after).Expanded);
    }

    private AppState OnDescriptionRow(AppState state, int row)
    {
        var pane = Document(state).SectionIds.ToList().IndexOf(PaneIds.Description);
        var steps = 0;

        while (Top(state).Cursor - Document(state).Sections[pane] != row && steps++ < 20)
        {
            state = Press(state, Top(state).Cursor - Document(state).Sections[pane] < row ? "j" : "k").State;
        }

        return state;
    }

    private static string ChipOf(AppState state)
    {
        var pane = Document(state).SectionIds.ToList().IndexOf(PaneIds.Description);

        return string.Concat(Document(state).PaneRightTitles[pane].Select(span => span.Text));
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

        while (!Document(state).Lines[Top(state).Cursor].PlainText.Contains(text) && steps++ < Document(state).Lines.Count + 100)
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
