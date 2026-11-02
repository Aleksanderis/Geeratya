using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.RemoteLinks.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>Cursor rows, section jumps and open keys of the issue detail, through the keymap and reducer.</summary>
[Trait("Os", "Independent")]
public class IssueDetailNavigationTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueTypeSummary _epic = new("11", "Epic", 1, false);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenCursorOnASubtaskRow_WhenPressingEnter_ThenItsDetailOpensAndEscComesBack()
    {
        var state = OnLine(Opened(), IsIssue("A-18"));

        var result = Press(state, "Enter");

        var tab = Active(result.State);
        Assert.Equal(["A-1", "A-18"], tab.Detail.Select(detail => detail.Key));
        Assert.Equal("A-18", Assert.Single(result.Effects.OfType<LoadIssueDetailEffect>()).Key);
        Assert.Equal(tab.Id, Assert.Single(result.Effects.OfType<LoadRemoteLinksEffect>()).Tab);

        var back = Press(result.State, "Esc").State;

        Assert.Equal(["A-1"], Active(back).Detail.Select(detail => detail.Key));
    }

    [Theory]
    [InlineData("A-30")]
    [InlineData("A-9")]
    [InlineData("A-21")]
    public void GivenCursorOnLinkedOrChildRow_WhenPressingEnter_ThenThatIssueOpens(string key)
    {
        var state = OnLine(Opened(), IsIssue(key));

        var result = Press(state, "Enter");

        Assert.Equal(key, Active(result.State).Detail[^1].Key);
        Assert.Equal(key, Assert.Single(result.Effects.OfType<LoadIssueDetailEffect>()).Key);
    }

    [Fact]
    public void GivenAPushedIssue_WhenTheReplyOfThePoppedIssueArrives_ThenItIsIgnored()
    {
        var pushed = Press(OnLine(Opened(), IsIssue("A-18")), "Enter").State;
        var tab = Active(pushed);
        var request = tab.Detail[^1].IssueRequest;
        var popped = Press(pushed, "Esc").State;

        var late = Deliver(popped, new IssueDetailLoaded(tab.Id, request, Detailed("A-18", _story), _now));

        Assert.Equal(popped, late);
    }

    [Fact]
    public void GivenCursorOnAPlainLine_WhenPressingEnter_ThenNoIssueOpens()
    {
        var state = Opened();

        var result = Press(state, "Enter");

        Assert.Single(Active(result.State).Detail);
        Assert.Empty(result.Effects.OfType<LoadIssueDetailEffect>());
    }

    [Fact]
    public void GivenCursorOnARemoteLinkRow_WhenOpening_ThenTheBrowserGetsTheUrlWithoutTheSiteHostCheck()
    {
        var state = OnLine(Opened(), IsRemoteLink);

        var viaKey = Press(state, "Space", "o");
        var viaEnter = Press(state, "Enter");

        foreach (var result in new[] { viaKey, viaEnter })
        {
            var effect = Assert.Single(result.Effects.OfType<OpenUrlEffect>());
            Assert.Equal("https://example.com/spec", effect.Url.AbsoluteUri);
        }
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://example.com/plain")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:secret@example.com/x")]
    [InlineData("not a url")]
    public void GivenHostileRemoteLink_WhenOpening_ThenItIsRefusedWithAMessage(string url)
    {
        var state = OnLine(Opened(url), IsRemoteLink);

        var result = Press(state, "Space", "o");

        Assert.Empty(result.Effects);
        Assert.StartsWith("Cannot open the link", result.State.Message);
    }

    [Fact]
    public void GivenRemoteLinkWithControlCharacters_WhenOpening_ThenItIsRefused()
    {
        var state = OnLine(Opened("https://example.com/a\u0007b"), IsRemoteLink);

        var result = Press(state, "Space", "o");

        Assert.Empty(result.Effects);
        Assert.Contains("control characters", result.State.Message);
    }

    [Fact]
    public void GivenCursorOnAnAttachment_WhenOpening_ThenTheIssuePageOpensNotTheFile()
    {
        var state = OnLine(Opened(), line => TargetKind(line) == DetailTargetKind.Attachment);

        var result = Press(state, "Space", "o");

        var effect = Assert.Single(result.Effects.OfType<OpenUrlEffect>());
        Assert.Equal("https://work.atlassian.net/browse/A-1", effect.Url.AbsoluteUri);
    }

    [Fact]
    public void GivenCursorOnARelatedIssue_WhenOpeningInTheBrowser_ThenThatIssuePageOpens()
    {
        var state = OnLine(Opened(), IsIssue("A-30"));

        var result = Press(state, "Space", "o");

        Assert.Equal(
            "https://work.atlassian.net/browse/A-30",
            Assert.Single(result.Effects.OfType<OpenUrlEffect>()).Url.AbsoluteUri);
    }

    [Fact]
    public void GivenAPushedIssueOnAPlainLine_WhenOpeningInTheBrowser_ThenTheShownIssueOpensNotTheListRow()
    {
        var pushed = Press(OnLine(Opened(), IsIssue("A-18")), "Enter").State;

        var result = Press(pushed, "Space", "o");

        Assert.Equal(
            "https://work.atlassian.net/browse/A-18",
            Assert.Single(result.Effects.OfType<OpenUrlEffect>()).Url.AbsoluteUri);
    }

    [Fact]
    public void GivenSectionJumps_WhenPressingNextRepeatedly_ThenTheCursorVisitsEverySectionInOrderAndStopsAtTheEnd()
    {
        var state = Opened();
        var sections = Document(state).Sections;
        var visited = new List<int>();

        for (var step = 0; step < sections.Count + 2; step++)
        {
            state = Press(Press(state, "]").State, "]").State;
            visited.Add(Top(state).Cursor);
        }

        Assert.True(sections.Count >= 6);
        Assert.Equal(sections.Skip(1), visited.Distinct());
        Assert.Equal(sections[^1], Top(state).Cursor);
    }

    [Fact]
    public void GivenSectionJumps_WhenPressingPrevious_ThenItGoesToTheStartOfTheCurrentThenThePreviousSection()
    {
        var state = Opened();
        var sections = Document(state).Sections;
        state = Press(state, "]", "]", "]", "]", "]", "]", "]", "]").State;
        state = Press(state, "j").State;
        var inside = Top(state).Cursor;

        var current = Press(state, "[", "[");
        var previous = Press(current.State, "[", "[");

        Assert.Equal(sections[4] + 1, inside);
        Assert.Equal(sections[4], Top(current.State).Cursor);
        Assert.Equal(sections[3], Top(previous.State).Cursor);
    }

    [Fact]
    public void GivenCursorInTheFirstSection_WhenPressingPreviousSection_ThenNothingMoves()
    {
        var state = Opened();

        var result = Press(Press(state, "[").State, "[");

        Assert.Equal(Top(state).Cursor, Top(result.State).Cursor);
    }

    [Fact]
    public void GivenSectionJump_WhenTheSectionIsFarDown_ThenTheWindowShowsItsTitleEdge()
    {
        var state = Opened();
        var sections = Document(state).Sections;

        var jumped = Press(Press(state, "]").State, "]").State;

        Assert.Equal(sections[1], Top(jumped).Cursor);
        Assert.Equal(sections[1] - 1, Top(jumped).Scroll);
    }

    [Fact]
    public void GivenIssueWithAParent_WhenPressingOpenParent_ThenTheParentOpens()
    {
        var state = Opened(parent: new IssueStub("A-5", "Parent epic", _todo, _epic));

        var result = Press(state, "Space", "p");

        Assert.Equal(["A-1", "A-5"], Active(result.State).Detail.Select(detail => detail.Key));
        Assert.Equal("A-5", Assert.Single(result.Effects.OfType<LoadIssueDetailEffect>()).Key);
    }

    [Fact]
    public void GivenIssueWithoutAParent_WhenPressingOpenParent_ThenTheStatusSaysSoAndNothingLoads()
    {
        var state = Opened();

        var result = Press(state, "Space", "p");

        Assert.Single(Active(result.State).Detail);
        Assert.Empty(result.Effects);
        Assert.Equal("A-1 has no parent.", result.State.Message);
    }

    [Fact]
    public void GivenCursorOnARelatedRow_WhenChildrenArriveAbove_ThenTheCursorStaysOnThatRow()
    {
        var withoutChildren = Opened(withChildren: false);
        var onRow = OnLine(withoutChildren, IsIssue("A-9"));
        var before = Top(onRow).Cursor;

        var after = ArriveChildren(onRow);

        Assert.NotEqual(before, Top(after).Cursor);
        Assert.True(IsKey(Document(after).Targets[Top(after).Cursor], "A-9"));
        Assert.Equal("A-9", Active(Press(after, "Enter").State).Detail[^1].Key);
    }

    [Fact]
    public void GivenAnIssueStillLoading_WhenPressingOpenParent_ThenTheParentOfTheListRowIsUsed()
    {
        var state = Press(ListWithParentRow(), "Enter").State;

        var result = Press(state, "Space", "p");

        Assert.Equal("A-5", Assert.Single(result.Effects.OfType<LoadIssueDetailEffect>()).Key);
    }

    [Fact]
    public void GivenNoIssueOpen_WhenPressingSectionOrParentKeys_ThenNothingHappensButAHint()
    {
        var state = ListWithParentRow();

        var section = Press(Press(state, "]").State, "]");
        var parent = Press(state, "Space", "p");

        Assert.Equal(state, section.State);
        Assert.Empty(parent.Effects);
        Assert.Equal("Open an issue first.", parent.State.Message);
    }

    [Fact]
    public void GivenTheDocument_WhenBuilt_ThenWrappedRowsKeepOneTargetAndPanelEdgesHaveNone()
    {
        var state = Opened(longSummary: true);
        var document = Document(state);

        var lines = document.Targets
            .Select((target, line) => (target, line))
            .Where(pair => IsKey(pair.target, "A-18"))
            .ToList();

        Assert.True(lines.Count > 1);
        Assert.Equal(lines.Count, lines[^1].line - lines[0].line + 1);
        Assert.All(
            document.Lines.Select((line, index) => (line, index)).Where(pair => pair.line.PlainText.StartsWith('╭')
                || pair.line.PlainText.StartsWith('╰')),
            pair => Assert.Null(document.Targets[pair.index]));
        Assert.Equal(document.Lines.Count, document.Targets.Count);
    }

    [Fact]
    public void GivenARelatedIssueWithAMalformedKey_WhenBuilding_ThenItsRowIsNotOpenable()
    {
        var issue = Detailed("A-1", _epic) with
        {
            Subtasks = [new IssueStub("not a key", "Odd", _todo, _story)],
        };

        var document = Document(Opened(issue: issue));

        Assert.DoesNotContain(document.Targets, target => IsKey(target, "not a key"));
    }

    [Fact]
    public void GivenAFullStack_WhenOpeningTheParent_ThenTheStatusSaysSoInsteadOfNothing()
    {
        var state = Opened(parent: new IssueStub("A-5", "Parent epic", _todo, _epic));
        var tab = Active(state);

        for (var index = 0; index < DetailPaging.MaxStack; index++)
        {
            state = Deliver(state, new IssueDetailOpenRequested(tab.Id, $"A-{index + 100}", null));
        }

        var loaded = Detailed("A-149", _epic) with { Parent = new IssueStub("A-5", "P", _todo, _epic) };

        state = Deliver(state, new IssueDetailLoaded(tab.Id, Top(state).IssueRequest, loaded, _now));

        var parent = Press(state, "Space", "p");

        Assert.Equal(DetailPaging.MaxStack, Active(parent.State).Detail.Count);
        Assert.Contains("Too many issues open", parent.State.Message);
        Assert.Empty(parent.Effects);
    }

    private static bool IsKey(DetailTarget? target, string key)
    {
        return target is { Kind: DetailTargetKind.Issue } && target.Value == key;
    }

    private static DetailTargetKind? TargetKind(DetailTarget? target)
    {
        return target?.Kind;
    }

    private static Func<DetailTarget?, bool> IsIssue(string key)
    {
        return target => IsKey(target, key);
    }

    private static bool IsRemoteLink(DetailTarget? target)
    {
        return target?.Kind == DetailTargetKind.RemoteLink;
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

    /// <summary>Moves the cursor down with <c>j</c> until <paramref name="match"/> holds for its target.</summary>
    private AppState OnLine(AppState state, Func<DetailTarget?, bool> match)
    {
        var steps = 0;

        while (!match(Document(state).Targets[Top(state).Cursor]) && steps++ < Document(state).Lines.Count)
        {
            state = Press(state, "j").State;
        }

        Assert.True(match(Document(state).Targets[Top(state).Cursor]), "no such row");

        return state;
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

    private AppState Deliver(AppState state, Command command)
    {
        return _reducer.Reduce(state, command).State;
    }

    private AppState ListWithParentRow()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var sized = _reducer.Reduce(start, new ViewportChanged(12, 80)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var parent = new IssueParent("A-5", "Parent epic");
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], parent, null, "A");
        var page = new IssuePage([row], null, null);

        return _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    /// <summary>An epic A-1 with every relation panel, loaded in a 12-row, 80-column tab.</summary>
    private AppState Opened(
        string remoteUrl = "https://example.com/spec",
        IssueStub? parent = null,
        bool longSummary = false,
        IssueDetail? issue = null,
        bool withChildren = true)
    {
        var state = Press(ListWithParentRow(), "Enter").State;
        var tab = Active(state);
        var detail = tab.Detail[0];
        var loaded = issue ?? Detailed("A-1", _epic) with
        {
            Parent = parent,
            Subtasks =
            [
                new IssueStub(
                    "A-18",
                    longSummary ? string.Join(' ', Enumerable.Repeat("a very long sub-task summary", 6)) : "Migration",
                    _todo,
                    _story),
            ],
            Links =
            [
                Link("1", IssueLinkDirection.Outward, "blocks", new IssueStub("A-30", "Launch", _todo, _story)),
                Link("2", IssueLinkDirection.Inward, "is blocked by", new IssueStub("A-9", "Legal", _todo, _story)),
            ],
            Attachments = [new IssueAttachment("1", "notes.txt", "text/plain", 47, _now, null, "https://x/content/1")],
        };

        state = Deliver(state, new IssueDetailLoaded(tab.Id, detail.IssueRequest, loaded, _now));
        var links = new[] { new RemoteLink("1", remoteUrl, "Design spec", "Confluence") };

        if (withChildren)
        {
            state = ArriveChildren(state);
        }

        state = Deliver(state, new IssueRemoteLinksLoaded(tab.Id, detail.LinksRequest, links, _now));

        return state;
    }

    private AppState ArriveChildren(AppState state)
    {
        var children = new IssuePage([ChildRow(), ChildRow(), ChildRow()], null, null);

        return Deliver(state, new IssueChildrenLoaded(Active(state).Id, Top(state).ChildrenRequest, children, _now));
    }

    private static IssueLink Link(string id, IssueLinkDirection direction, string text, IssueStub issue)
    {
        return new IssueLink(id, "Blocks", direction, text, issue);
    }

    private static IssueSummary ChildRow()
    {
        return new IssueSummary("21", "A-21", "Child", _todo, _story, null, null, [], null, null, "A");
    }

    private static IssueDetail Detailed(string key, IssueTypeSummary type)
    {
        return new IssueDetail(
            "1",
            key,
            "Issue 1",
            _todo,
            type,
            "High",
            null,
            null,
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
}
