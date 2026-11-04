using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>Long text starts cut short and sections fold to a preview; the state is per issue (#678).</summary>
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
        Assert.Contains(text.Split('\n'), line => line.Contains("more lines (Enter)"));
    }

    [Fact]
    public void GivenAShortDescription_WhenOpening_ThenNothingIsCutAndNoMoreLineShows()
    {
        var state = Opened(LongText(3), []);

        Assert.DoesNotContain("more line", Text(state));
        Assert.DoesNotContain("Show less", Text(state));
    }

    [Fact]
    public void GivenTheMoreLine_WhenPressingEnterTwice_ThenTheDescriptionExpandsAndCutsShortAgain()
    {
        var state = OnLine(Opened(LongText(20), []), "more lines (Enter)");

        var expanded = Press(state, "Enter").State;

        Assert.Contains("line 20", Text(expanded));
        Assert.Contains("Show less (Enter)", Text(expanded));
        Assert.Equal(["description"], Top(expanded).Expanded);

        var again = Press(OnLine(expanded, "Show less (Enter)"), "Enter").State;

        Assert.DoesNotContain("line 20", Text(again));
        Assert.Empty(Top(again).Expanded);
    }

    [Fact]
    public void GivenShowLess_WhenPressingEnter_ThenTheCursorLandsOnTheMoreLineNotInALaterPane()
    {
        var expanded = Press(OnLine(Opened(LongText(30), [Comment("7", 3)]), "more lines (Enter)"), "Enter").State;

        var collapsed = Press(OnLine(expanded, "Show less (Enter)"), "Enter").State;

        Assert.Contains("more lines (Enter)", Document(collapsed).Lines[Top(collapsed).Cursor].PlainText);
        Assert.True(Top(collapsed).Cursor < Document(collapsed).Sections[2]);
    }

    [Fact]
    public void GivenAFoldedCommentsPane_WhenTheCursorIsOnItsFirstLine_ThenTheCommentKeysStillFindTheComment()
    {
        var state = OnLine(Opened(null, [Comment("7", 3), Comment("8", 3)]), "Eve Example ·");

        var folded = Press(state, "z", "a").State;
        var cursor = Document(folded).Targets[Top(folded).Cursor];

        Assert.Equal(new DetailTarget(DetailTargetKind.Comment, "7"), cursor);
    }

    [Fact]
    public void GivenARightClickOnAPaneTitle_WhenResolving_ThenTheSectionDoesNotFold()
    {
        var state = Opened(LongText(20), []);
        var edge = Document(state).Sections[1] - 1;
        var row = 2 + DetailLayout.HeaderRows + edge - Top(state).Scroll;
        var context = MouseContext.From(state, 80, 30, 2, _now);
        var current = state;

        foreach (var command in MouseResolver.Resolve(context, 4, row, MouseGesture.RightClick, _now))
        {
            current = _reducer.Reduce(current, command).State;
        }

        Assert.Empty(Top(current).Folded);
    }

    [Fact]
    public void GivenAnExpandedDescription_WhenTheIssueReloads_ThenItStaysExpanded()
    {
        var state = Press(OnLine(Opened(LongText(20), []), "more lines (Enter)"), "Enter").State;
        var reloaded = Deliver(state, new IssueDetailLoaded(Active(state).Id, Top(state).IssueRequest, Issue(LongText(20)), _now));

        Assert.Contains("line 20", Text(reloaded));
    }

    [Fact]
    public void GivenALongComment_WhenOpening_ThenItIsCutAndTheCommentKeysStillFindItOnTheMoreLine()
    {
        var state = Opened(null, [Comment("7", 14)]);
        var onMore = OnLine(state, "more lines (Enter)");

        var target = Document(onMore).Targets[Top(onMore).Cursor]!;

        Assert.Equal(DetailTargetKind.Comment, target.Kind);
        Assert.Equal("7", target.Value);
        Assert.DoesNotContain("item 14", Text(state));

        var expanded = Press(onMore, "Enter").State;

        Assert.Contains("item 14", Text(expanded));
        Assert.Equal(["comment:7"], Top(expanded).Expanded);
    }

    [Fact]
    public void GivenTheCursorInTheDescription_WhenPressingZa_ThenTheSectionFoldsToAPreviewAndOpensAgain()
    {
        var state = OnLine(Opened(LongText(20), []), "line 2");

        var folded = Press(state, "z", "a").State;

        Assert.Contains("Description (folded)", Text(folded));
        Assert.DoesNotContain("line 3", Text(folded));
        Assert.Contains("more lines hidden", Text(folded));
        Assert.Equal(["Description"], Top(folded).Folded);

        var open = Press(folded, "z", "a").State;

        Assert.DoesNotContain("(folded)", Text(open));
        Assert.Contains("line 3", Text(open));
    }

    [Fact]
    public void GivenAFoldedSection_WhenTheIssueReloads_ThenItStaysFolded()
    {
        var state = Press(OnLine(Opened(LongText(20), []), "line 2"), "z", "a").State;

        var reloaded = Deliver(state, new IssueDetailLoaded(Active(state).Id, Top(state).IssueRequest, Issue(LongText(20)), _now));

        Assert.Contains("Description (folded)", Text(reloaded));
    }

    [Fact]
    public void GivenTheCursorInTheFields_WhenPressingZa_ThenNothingFoldsAndTheStatusLineSaysSo()
    {
        var result = Press(Opened(LongText(20), []), "z", "a");

        Assert.Equal("The fields cannot be folded.", result.State.Message);
        Assert.Empty(Top(result.State).Folded);
    }

    [Fact]
    public void GivenAClickOnAPaneTitle_WhenResolving_ThenThatSectionFolds()
    {
        var state = Opened(LongText(20), []);
        var document = Document(state);
        var edge = document.Sections[1] - 1;
        var row = 2 + DetailLayout.HeaderRows + edge - Top(state).Scroll;
        var context = MouseContext.From(state, 80, 30, 2, _now);
        var current = state;

        foreach (var command in MouseResolver.Resolve(context, 4, row, MouseGesture.Click, _now))
        {
            current = _reducer.Reduce(current, command).State;
        }

        Assert.Equal(["Description"], Top(current).Folded);
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
