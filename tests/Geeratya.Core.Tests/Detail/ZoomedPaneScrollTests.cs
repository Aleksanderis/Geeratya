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

/// <summary>Zoomed panes other than Comments scroll inside their frame (#740).</summary>
[Trait("Os", "Independent")]
public class ZoomedPaneScrollTests
{
    private const int SubtaskCount = 40;
    private const int FirstSubtask = 100;

    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenALongSubtaskList_WhenZoomed_ThenTheFrameStaysAndTheScrollbarShows()
    {
        var zoomed = Zoomed("subtasks");
        var document = Document(zoomed);
        var scroll = document.PaneScrolls.Single();
        var chip = document.PaneRightTitles.Single().Single().Text;

        Assert.NotNull(scroll);
        Assert.True(scroll.Scrollable);
        Assert.Equal(document.ViewportRows, document.Lines.Count);
        Assert.Equal(SubtaskCount, scroll.Total);
        Assert.StartsWith("1-", chip);
        Assert.EndsWith($"of {SubtaskCount}", chip);
    }

    [Fact]
    public void GivenAZoomedSubtaskList_WhenMovingPastTheLastShownRow_ThenTheWindowScrollsAndTheCursorFollows()
    {
        var zoomed = Zoomed("subtasks");
        var shown = Document(zoomed).PaneScrolls.Single()!.Viewport;
        var moved = Press(zoomed, [.. Enumerable.Repeat("j", shown)]);
        var offset = Document(moved).PaneScrolls.Single()!.ClampedOffset;

        Assert.True(offset > 0);
        Assert.Equal(offset, Top(moved).ZoomOffset);
        Assert.Equal(Document(zoomed).Lines.Count, Document(moved).Lines.Count);
        Assert.Contains($"A-{FirstSubtask + shown}", Document(moved).Lines[Top(moved).Cursor].PlainText);
    }

    [Fact]
    public void GivenAZoomedSubtaskList_WhenGoingToTheEndAndBack_ThenTheOffsetIsClampedAndReturnsToZero()
    {
        var zoomed = Zoomed("subtasks");
        var bottom = Press(zoomed, "G");
        var scroll = Document(bottom).PaneScrolls.Single()!;

        Assert.Equal(scroll.MaxOffset, scroll.ClampedOffset);
        Assert.Contains($"A-{FirstSubtask + SubtaskCount - 1}", Document(bottom).Lines[Top(bottom).Cursor].PlainText);

        var past = Press(bottom, "j", "j");

        Assert.Equal(scroll.MaxOffset, Top(past).ZoomOffset);

        var top = Press(past, "g", "g");

        Assert.Equal(0, Top(top).ZoomOffset);
    }

    [Fact]
    public void GivenAZoomedDescription_WhenItIsLong_ThenItScrollsInsideItsFrame()
    {
        var zoomed = Zoomed("description");
        var scroll = Document(zoomed).PaneScrolls.Single();

        Assert.NotNull(scroll);
        Assert.True(scroll.Scrollable);
        Assert.Equal(Document(zoomed).ViewportRows, Document(zoomed).Lines.Count);

        var moved = Press(zoomed, "G");

        Assert.True(Top(moved).ZoomOffset > 0);
    }

    [Fact]
    public void GivenAZoomedDetailsPane_WhenItFits_ThenNoScrollbarShows()
    {
        var zoomed = Zoomed("details");

        Assert.Null(Document(zoomed).PaneScrolls.Single());
    }

    [Fact]
    public void GivenAScrolledZoomedPane_WhenSwitchingSectionOrLeavingTheZoom_ThenTheOffsetIsReset()
    {
        var scrolled = Press(Zoomed("subtasks"), "G");

        Assert.True(Top(scrolled).ZoomOffset > 0);
        Assert.Equal(0, Top(Press(scrolled, "Space", "z")).ZoomOffset);
        Assert.Equal(0, Top(Press(scrolled, "Ctrl-l")).ZoomOffset);
    }

    [Fact]
    public void GivenAnOffsetPastTheEnd_WhenLayingOut_ThenTheWindowIsClampedAndFillsTheFrame()
    {
        var zoomed = Zoomed("subtasks");
        var detail = Top(zoomed) with { ZoomOffset = 10_000 };
        var document = IssueDocumentBuilder.Build(detail, Document(zoomed).Width, Document(zoomed).ViewportRows);
        var scroll = document.PaneScrolls.Single()!;

        Assert.Equal(scroll.MaxOffset, scroll.ClampedOffset);
        Assert.Equal(document.ViewportRows, document.Lines.Count);
    }

    private AppState Zoomed(string pane)
    {
        var state = Opened();
        var steps = 0;

        while (Document(state).SectionIds[Document(state).SectionAt(Top(state).Cursor)] != pane && steps++ < 20)
        {
            state = Press(state, "Ctrl-j");
        }

        return Press(state, "Space", "z");
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

    private AppState Deliver(AppState state, Command command)
    {
        return _reducer.Reduce(state, command).State;
    }

    private AppState Press(AppState state, params string[] keys)
    {
        var current = state;

        foreach (var key in keys)
        {
            current = _reducer.Reduce(current, new KeyPressed(key)).State;
        }

        return current;
    }

    private AppState Opened()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var sized = _reducer.Reduce(start, new ViewportChanged(30, 80)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");
        var page = new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([row], null, null), _now);
        var shown = Press(Deliver(opened.State, page), "Enter");
        var detail = Active(shown).Detail[0];
        var loaded = Deliver(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(), _now));
        var comments = new OffsetPage<IssueComment>([], 0, DetailPaging.CommentPageSize, 0, true);

        return Deliver(loaded, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, comments, _now));
    }

    private static IssueDetail Issue()
    {
        var subtasks = Enumerable.Range(FirstSubtask, SubtaskCount)
            .Select(number => new IssueStub($"A-{number}", $"Part {number}", _todo, _story))
            .ToList();
        var text = string.Join("\n\n", Enumerable.Range(1, 40).Select(number => $"Paragraph {number}."));

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
            AdfMarkdownParser.Parse(text),
            null,
            subtasks,
            [],
            []);
    }
}
