using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>
/// Status and assignee of a sub-task from the parent's detail (#990): the keys act on the sub-task under the cursor, the
/// parent reloads after the write or when it comes back, and the refresh key reads the shown issue again.
/// </summary>
[Trait("Os", "Independent")]
public class SubtaskWriteTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueTypeSummary _sub = new("11", "Sub-task", -1, true);
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenTheCursorOnASubtask_WhenPressingTheStatusKey_ThenThePickerOpensForTheSubtask()
    {
        var state = OnLine(Opened(), "A-2");

        var result = Press(state, "Space", "i", "s");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal("A-2", effect.Target.IssueKey);
    }

    [Fact]
    public void GivenTheCursorOnTheHeader_WhenPressingTheStatusKey_ThenThePickerOpensForTheShownIssue()
    {
        var result = Press(Opened(), "Space", "i", "s");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal("A-1", effect.Target.IssueKey);
    }

    [Fact]
    public void GivenTheCursorOnASubtask_WhenPressingTheAssignKey_ThenThePeoplePickerTargetsTheSubtask()
    {
        var state = OnLine(Opened(), "A-2");

        var result = Press(state, "Space", "i", "a");

        Assert.Equal("A-2", Active(result.State).Form!.Target.IssueKey);
    }

    [Fact]
    public void GivenTheCursorOnASubtask_WhenAssigningToMe_ThenTheSubtaskIsWrittenAndTheParentReloads()
    {
        var state = OnLine(Opened(), "A-2");

        var asked = Press(state, "Space", "i", "m");

        var write = Assert.IsType<ExecuteWriteEffect>(Assert.Single(asked.Effects, effect => effect is ExecuteWriteEffect));
        Assert.Equal("A-2", Assert.IsType<AssignRequest>(write.Request).Key);

        var operation = Assert.Single(asked.State.Session.Operations);
        var done = _reducer.Reduce(asked.State, new WriteSucceeded(operation.Id, _now, []));

        var reload = Assert.IsType<LoadIssueDetailEffect>(Assert.Single(done.Effects, effect => effect is LoadIssueDetailEffect));
        Assert.Equal("A-1", reload.Key);
    }

    [Fact]
    public void GivenTheSubtaskWasWrittenInItsOwnDetail_WhenGoingBack_ThenTheParentReloads()
    {
        var parent = Opened();
        var inSubtask = OpenSubtask(OnLine(parent, "A-2"));
        var asked = Press(inSubtask, "Space", "i", "m");
        var operation = Assert.Single(asked.State.Session.Operations);
        var done = _reducer.Reduce(asked.State, new WriteSucceeded(operation.Id, _now, []));

        Assert.DoesNotContain(done.Effects, effect => effect is LoadIssueDetailEffect { Key: "A-1" });

        Assert.True(Active(done.State).Detail[0].IssueStale);

        // The first Esc dismisses the status message of the write, the second goes back.
        var back = Press(done.State, "Esc", "Esc");

        var reload = Assert.IsType<LoadIssueDetailEffect>(Assert.Single(back.Effects, effect => effect is LoadIssueDetailEffect));
        Assert.Equal("A-1", reload.Key);
        Assert.Single(Active(back.State).Detail);
    }

    [Fact]
    public void GivenAnOpenedIssue_WhenPressingTheRefreshKey_ThenTheIssueAndItsCommentsLoadAgain()
    {
        var result = Press(Opened(), "Space", "r");

        Assert.Contains(result.Effects, effect => effect is LoadIssueDetailEffect { Key: "A-1" });
        Assert.Contains(result.Effects, effect => effect is LoadIssueCommentsEffect { Key: "A-1" });
        Assert.Contains(result.Effects, effect => effect is LoadViewPageEffect);
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static DetailState Top(AppState state)
    {
        return Active(state).Detail[^1];
    }

    private AppState OnLine(AppState state, string text)
    {
        var steps = 0;

        while (!Top(state).Document!.Lines[Top(state).Cursor].PlainText.Contains(text, StringComparison.Ordinal)
            && steps++ < Top(state).Document!.Lines.Count + 100)
        {
            state = Press(state, "j").State;
        }

        Assert.Contains(text, Top(state).Document!.Lines[Top(state).Cursor].PlainText, StringComparison.Ordinal);

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

    // Enter on the sub-task row pushes it; its own issue reply arrives so that its document is laid out.
    private AppState OpenSubtask(AppState state)
    {
        var pushed = Press(state, "Enter").State;
        var tab = Active(pushed);
        var detail = tab.Detail[^1];
        var issue = Issue("A-2", "Sub work", _sub, null, []);

        return _reducer.Reduce(pushed, new IssueDetailLoaded(tab.Id, detail.IssueRequest, issue, _now)).State;
    }

    private AppState Opened()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me Myself", "acc-me") };
        var start = AppState.Initial with { Profiles = profiles, Session = session };
        var sized = _reducer.Reduce(start, new ViewportChanged(9, 80)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");
        var listed = _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([row], null, null), _now)).State;
        var shown = Press(listed, "Enter").State;
        var detail = Active(shown).Detail[0];
        var stub = new IssueStub("A-2", "Sub work", _todo, _sub);
        var loaded = _reducer.Reduce(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue("A-1", "Issue 1", _story, stub, [stub]), _now)).State;
        var commentPage = new OffsetPage<IssueComment>([], 0, DetailPaging.CommentPageSize, 0, true);

        return _reducer.Reduce(loaded, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, commentPage, _now)).State;
    }

    private static IssueDetail Issue(string key, string summary, IssueTypeSummary type, IssueStub? parent, IReadOnlyList<IssueStub> subtasks)
    {
        return new IssueDetail(
            "1",
            key,
            summary,
            _todo,
            type,
            "High",
            _eve,
            _eve,
            [],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            null,
            parent,
            subtasks,
            [],
            []);
    }
}
