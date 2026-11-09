using Geeratya.Core.Clipboard;
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

/// <summary>Select and copy text in the issue detail (#773): the keys through the real reducer.</summary>
[Trait("Os", "Independent")]
public class DetailYankTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenVisualMode_WhenMovingDownAndPressingY_ThenTheSelectedLinesAreCopiedWithoutTheFrame()
    {
        var state = OnLine(Opened(Paragraphs(3), []), "line 1");

        var started = Press(state, "v");
        var yanked = Press(started.State, "j", "j", "y");

        var copy = Assert.IsType<CopyToClipboardEffect>(Assert.Single(yanked.Effects));
        Assert.Equal("line 1\n\nline 2", copy.Text);
        Assert.Equal("3 lines", copy.Description);
        Assert.Null(Top(yanked.State).YankAnchor);
    }

    [Fact]
    public void GivenVisualMode_WhenMovingUpPastTheAnchor_ThenTheRangeRunsUpwards()
    {
        var state = OnLine(Opened(Paragraphs(3), []), "line 2");

        var yanked = Press(state, "v", "k", "k", "y");

        Assert.Equal("line 1\n\nline 2", Assert.IsType<CopyToClipboardEffect>(Assert.Single(yanked.Effects)).Text);
    }

    [Fact]
    public void GivenASelectionInTheDescription_WhenMovingToTheBottom_ThenItStopsAtTheEndOfThePane()
    {
        var state = OnLine(Opened(Paragraphs(3), [Comment("7", 2)]), "line 1");

        var yanked = Press(state, "v", "G", "y");

        var text = Assert.IsType<CopyToClipboardEffect>(Assert.Single(yanked.Effects)).Text;
        Assert.Equal("line 1\n\nline 2\n\nline 3", text);
        Assert.DoesNotContain("item", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoSelection_WhenPressingYOnACutDescription_ThenTheWholeTextIsCopied()
    {
        var state = OnLine(Opened(Paragraphs(20), []), "line 1");

        var yanked = Press(state, "y");

        var text = Assert.IsType<CopyToClipboardEffect>(Assert.Single(yanked.Effects)).Text;
        Assert.StartsWith("line 1\n\nline 2", text, StringComparison.Ordinal);
        Assert.EndsWith("line 20", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoSelection_WhenPressingYOnACutComment_ThenTheWholeCommentBodyIsCopied()
    {
        var state = Press(Opened(null, [Comment("7", 14)]), "G").State;

        var yanked = Press(state, "y");

        var text = Assert.IsType<CopyToClipboardEffect>(Assert.Single(yanked.Effects)).Text;
        Assert.Contains("item 14", text, StringComparison.Ordinal);
        Assert.Contains("item 1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenACutDescription_WhenPressingV_ThenItIsShownInFullAndTheSelectionStarts()
    {
        var state = OnLine(Opened(Paragraphs(20), []), "line 1");

        var started = Press(state, "v").State;

        Assert.Equal(["description"], Top(started).Expanded);
        Assert.NotNull(Top(started).YankAnchor);
        Assert.Contains("line 20", string.Join('\n', Document(started).Lines.Select(line => line.PlainText)));
    }

    [Fact]
    public void GivenASelection_WhenPressingEsc_ThenItEndsAndTheIssueStaysOpen()
    {
        var state = Press(OnLine(Opened(Paragraphs(3), []), "line 1"), "v").State;

        var escaped = Press(state, "Esc").State;

        Assert.Null(Top(escaped).YankAnchor);
        Assert.Single(Active(escaped).Detail);
    }

    [Fact]
    public void GivenASelection_WhenPressingVAgain_ThenItEndsWithoutCopying()
    {
        var state = Press(OnLine(Opened(Paragraphs(3), []), "line 1"), "v");

        var ended = Press(state.State, "v");

        Assert.Null(Top(ended.State).YankAnchor);
        Assert.Empty(ended.Effects);
    }

    [Fact]
    public void GivenASelection_WhenTheIssueReloads_ThenTheSelectionEndsBecauseItsLinesMoved()
    {
        var state = Press(OnLine(Opened(Paragraphs(3), []), "line 1"), "v").State;

        var reloaded = _reducer.Reduce(
            state,
            new IssueDetailLoaded(Active(state).Id, Top(state).IssueRequest, Issue(Paragraphs(3)), _now)).State;

        Assert.Null(Top(reloaded).YankAnchor);
    }

    [Fact]
    public void GivenAFinishedCopy_WhenTheHostReportsIt_ThenTheStatusSaysWhatWasCopiedOrWhyNot()
    {
        var state = Opened(Paragraphs(2), []);

        var copied = _reducer.Reduce(state, new ClipboardCopied("2 lines", true, "wl-copy")).State;
        var failed = _reducer.Reduce(state, new ClipboardCopied("2 lines", false, "no clipboard tool found")).State;

        Assert.Equal("Copied 2 lines (wl-copy)", copied.Message);
        Assert.Equal("Cannot copy: no clipboard tool found", failed.Message);
        Assert.Equal(StatusSeverity.Warning, failed.Status!.Severity);
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenLookingUpY_ThenItIsTheYankActionInNormalMode()
    {
        var keymap = DefaultKeymap.Load();

        var resolution = keymap.Resolve(InputMode.Normal, string.Empty, "y");

        Assert.Equal(ActionIds.DetailYank, resolution.Action);
    }

    [Fact]
    public void GivenAListWithNoIssueOpen_WhenPressingY_ThenTheSelectedIssueKeyIsCopied()
    {
        var state = Opened(Paragraphs(2), []);
        var closed = Press(state, "Esc").State;

        var result = Press(closed, "y");

        var copy = Assert.IsType<CopyToClipboardEffect>(Assert.Single(result.Effects));
        Assert.Equal("A-1", copy.Text);
    }

    [Fact]
    public void GivenAListRow_WhenYankingTheLink_ThenTheBrowseAddressIsCopied()
    {
        var closed = Press(Opened(Paragraphs(2), []), "Esc").State;

        var result = Press(closed, "Space", "y", "u");

        var copy = Assert.IsType<CopyToClipboardEffect>(Assert.Single(result.Effects));
        Assert.Equal("https://work.atlassian.net/browse/A-1", copy.Text);
    }

    [Fact]
    public void GivenAnOpenedIssue_WhenYankingKeyAndSummary_ThenBothAreCopied()
    {
        var result = Press(Opened(Paragraphs(2), []), "Space", "y", "s");

        var copy = Assert.IsType<CopyToClipboardEffect>(Assert.Single(result.Effects));
        Assert.Equal("A-1 Issue 1", copy.Text);
    }

    [Fact]
    public void GivenTheCursorOnASubtask_WhenYankingTheKey_ThenTheSubtaskKeyIsCopied()
    {
        var subtask = new IssueStub("A-2", "Sub work", _todo, _story);
        var state = OnLine(Opened(Paragraphs(2), [], subtask), "A-2");

        var result = Press(state, "Space", "y", "y");

        var copy = Assert.IsType<CopyToClipboardEffect>(Assert.Single(result.Effects));
        Assert.Equal("A-2", copy.Text);
    }

    [Fact]
    public void GivenTheCursorOnASubtask_WhenYankingKeyAndSummary_ThenTheSubtaskSummaryIsIncluded()
    {
        var subtask = new IssueStub("A-2", "Sub work", _todo, _story);
        var state = OnLine(Opened(Paragraphs(2), [], subtask), "A-2");

        var result = Press(state, "Space", "y", "s");

        var copy = Assert.IsType<CopyToClipboardEffect>(Assert.Single(result.Effects));
        Assert.Equal("A-2 Sub work", copy.Text);
    }

    [Fact]
    public void GivenMarkedRows_WhenYankingTheKey_ThenNothingIsCopiedAndTheStatusSaysWhy()
    {
        var closed = Press(Opened(Paragraphs(2), []), "Esc").State;
        var visual = Press(closed, "Alt-m").State;

        var result = Press(visual, "y");

        Assert.Empty(result.Effects);
        Assert.Contains("Copy takes one issue", result.State.Message);
    }

    [Fact]
    public void GivenAProfileWithNoSite_WhenYankingTheLink_ThenNothingIsCopiedAndTheStatusWarns()
    {
        var closed = Press(Opened(Paragraphs(2), []), "Esc").State;
        var state = closed with { Profiles = ProfileCatalog.Empty };

        var result = Press(state, "Space", "y", "u");

        Assert.Empty(result.Effects);
        Assert.Contains("no active site", result.State.Message);
    }

    [Fact]
    public void GivenAnEmptyView_WhenYankingTheKey_ThenNothingIsCopiedAndTheStatusWarns()
    {
        var closed = Press(Opened(Paragraphs(2), []), "Esc").State;
        var emptied = closed;
        var tab = Active(emptied);
        var cleared = tab with { Rows = [] };
        var tabs = emptied.Session.Tabs.Select(candidate => candidate.Id == tab.Id ? cleared : candidate).ToList();
        var state = emptied with { Session = emptied.Session with { Tabs = tabs } };

        var result = Press(state, "Space", "y", "y");

        Assert.Empty(result.Effects);
        Assert.Contains("No issue selected", result.State.Message);
    }

    [Fact]
    public void GivenAScrolledCommentsPane_WhenSelectingTheCursorRow_ThenTheCopyIsThatRowNotOneFromTheTop()
    {
        IReadOnlyList<IssueComment> comments = [.. Enumerable.Range(1, 12).Select(number => Comment($"c{number}", 1))];
        var state = Press(Opened(null, comments), "G").State;
        var shown = Document(state).Lines[Top(state).Cursor];

        var yanked = Press(state, "v", "y");

        var text = Assert.IsType<CopyToClipboardEffect>(Assert.Single(yanked.Effects)).Text;
        Assert.Equal(DetailText.Of(shown), text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public void GivenAZoomedPaneScrolledDown_WhenSelectingTheLastRows_ThenTheCopyHoldsThoseRows()
    {
        var state = OnLine(Opened(Paragraphs(40), []), "line 1");
        var zoomed = Press(state, "Space", "z", "G").State;

        var yanked = Press(zoomed, "v", "k", "k", "y");

        var text = Assert.IsType<CopyToClipboardEffect>(Assert.Single(yanked.Effects)).Text;
        Assert.Equal("line 39\n\nline 40", text);
    }

    private static string Paragraphs(int count)
    {
        return string.Join("\n\n", Enumerable.Range(1, count).Select(number => $"line {number}"));
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

        while (!Document(state).Lines[Top(state).Cursor].PlainText.Contains(text, StringComparison.Ordinal)
            && steps++ < Document(state).Lines.Count + 100)
        {
            state = Press(state, "j").State;
        }

        Assert.Contains(text, Document(state).Lines[Top(state).Cursor].PlainText, StringComparison.Ordinal);

        return state;
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);
        var effects = new List<Effect>();

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key));
            effects.AddRange(result.Effects);
        }

        return new ReduceResult(result.State, effects);
    }

    private AppState Opened(
        string? description,
        IReadOnlyList<IssueComment> comments,
        IssueStub? subtask = null)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var sized = _reducer.Reduce(start, new ViewportChanged(9, 80)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");
        var page = new IssuePage([row], null, null);
        var listed = _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
        var shown = Press(listed, "Enter").State;
        var detail = Active(shown).Detail[0];
        var issue = new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(description, subtask), _now);
        var loaded = _reducer.Reduce(shown, issue).State;
        var commentPage = new OffsetPage<IssueComment>(comments, 0, DetailPaging.CommentPageSize, comments.Count, true);

        var delivered = new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, commentPage, _now);

        return _reducer.Reduce(loaded, delivered).State;
    }

    private static IssueDetail Issue(string? description, IssueStub? subtask = null)
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
            subtask is null ? [] : [subtask],
            [],
            []);
    }

    private static IssueComment Comment(string id, int items)
    {
        var markdown = string.Join('\n', Enumerable.Range(1, items).Select(number => $"- item {number}"));

        return new IssueComment(id, _eve, AdfMarkdownParser.Parse(markdown), _now.AddHours(-2), _now.AddHours(-2));
    }
}
