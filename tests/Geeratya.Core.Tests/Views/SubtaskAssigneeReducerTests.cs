using Geeratya.Core.Boards;
using Geeratya.Core.Detail;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class SubtaskAssigneeReducerTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    [Fact]
    public void GivenAnExpandedCard_WhenTheAssigneesArrive_ThenTheListedRowsCarryThemAndUnassignedIsKnown()
    {
        var state = Start();
        var jane = new IssueAssignee("a1", "Jane Doe");
        var loaded = new SubtaskAssigneesLoaded(
            new TabId(1),
            "A-1",
            [new SubtaskAssignee("A-2", jane), new SubtaskAssignee("A-3", null)]);

        var next = SubtaskAssigneeReducer.Loaded(state, loaded);
        var parent = next.Session.Tabs[0].Rows[0];
        var listed = parent.Subtasks.Select(stub => BoardColumns.Listed(parent, stub)).ToArray();

        Assert.Equal(jane, listed[0].Assignee);
        Assert.True(listed[0].AssigneeLoaded);
        Assert.Null(listed[1].Assignee);
        Assert.True(listed[1].AssigneeLoaded);
    }

    [Fact]
    public void GivenAStubNotInTheAnswer_WhenTheAssigneesArrive_ThenItStaysUnknown()
    {
        var loaded = new SubtaskAssigneesLoaded(new TabId(1), "A-1", [new SubtaskAssignee("A-2", null)]);

        var parent = SubtaskAssigneeReducer.Loaded(Start(), loaded).Session.Tabs[0].Rows[0];

        Assert.False(BoardColumns.Listed(parent, parent.Subtasks[1]).AssigneeLoaded);
    }

    [Fact]
    public void GivenAClosedTabOrUnknownParent_WhenTheAssigneesArrive_ThenTheStateIsUntouched()
    {
        var state = Start();

        Assert.Same(state, SubtaskAssigneeReducer.Loaded(state, new SubtaskAssigneesLoaded(new TabId(99), "A-1", [])));
        Assert.Same(state, SubtaskAssigneeReducer.Loaded(state, new SubtaskAssigneesLoaded(new TabId(1), "Z-9", [])));
    }

    [Fact]
    public void GivenARefreshOfAnExpandedCard_WhenRowsAreMerged_ThenTheKnownAssigneesAreCarriedOver()
    {
        var jane = new IssueAssignee("a1", "Jane Doe");
        var old = SubtaskAssigneeReducer.Loaded(
            Start(),
            new SubtaskAssigneesLoaded(new TabId(1), "A-1", [new SubtaskAssignee("A-2", jane)]));
        var oldRows = old.Session.Tabs[0].Rows;
        var fresh = Start().Session.Tabs[0].Rows;

        var carried = SubtaskAssigneeReducer.CarryOver(oldRows, fresh, ["A-1"]);

        Assert.Equal(jane, carried[0].Subtasks[0].Assignee);
        Assert.True(carried[0].Subtasks[0].AssigneeKnown);
        Assert.False(carried[0].Subtasks[1].AssigneeKnown);
    }

    [Fact]
    public void GivenACardThatIsNotExpanded_WhenRowsAreMerged_ThenNothingIsCarriedOver()
    {
        var old = SubtaskAssigneeReducer.Loaded(
            Start(),
            new SubtaskAssigneesLoaded(new TabId(1), "A-1", [new SubtaskAssignee("A-2", null)]));
        var fresh = Start().Session.Tabs[0].Rows;

        Assert.Same(fresh, SubtaskAssigneeReducer.CarryOver(old.Session.Tabs[0].Rows, fresh, []));
        Assert.False(SubtaskAssigneeReducer.CarryOver(old.Session.Tabs[0].Rows, fresh, ["A-9"])[0].Subtasks[0].AssigneeKnown);
    }

    [Fact]
    public void GivenACardWithSubtasks_WhenExpandingAndCollapsing_ThenOnlyExpandingLoadsAssignees()
    {
        var state = Start();

        var expanded = SubtaskActions.Toggle(state)!;
        var collapsed = SubtaskActions.Toggle(expanded.State)!;

        Assert.Equal([new LoadSubtaskAssigneesEffect(new TabId(1), "A-1")], expanded.Effects);
        Assert.Empty(collapsed.Effects);
    }

    [Theory]
    [InlineData(LoadReason.Refresh)]
    [InlineData(LoadReason.Retry)]
    public void GivenAnExpandedCard_WhenARefreshPageLoads_ThenTheChipsStayAndNoRequestIsSent(
        LoadReason reason)
    {
        var jane = new IssueAssignee("a1", "Jane Doe");
        var loaded = SubtaskAssigneeReducer.Loaded(
            Start(),
            new SubtaskAssigneesLoaded(new TabId(1), "A-1", [new SubtaskAssignee("A-2", jane)]));
        var tab = loaded.Session.Tabs[0] with
        {
            ExpandedSubtasks = ["A-1"],
            Load = new Loading(reason),
            RequestId = 7,
        };
        var state = loaded with { Session = loaded.Session with { Tabs = [tab] } };
        var page = new IssuePage(Start().Session.Tabs[0].Rows, null, null);

        var result = ViewReducer.Reduce(state, new ViewPageLoaded(tab.Id, 7, page, DateTimeOffset.UnixEpoch));

        Assert.True(result.State.Session.Tabs[0].Rows[0].Subtasks[0].AssigneeKnown);
        Assert.DoesNotContain(result.Effects, effect => effect is LoadSubtaskAssigneesEffect);
    }

    private static AppState Start()
    {
        var status = new IssueStatus("1", "To Do", "new");
        var parent = new IssueSummary(
            "1",
            "A-1",
            "Parent",
            status,
            new IssueTypeSummary("10", "Story", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A")
        {
            Subtasks =
            [
                new IssueSubtask("A-2", "One", status, "Sub-task"),
                new IssueSubtask("A-3", "Two", status, "Sub-task"),
            ],
        };
        var tab = TabState.Opened(
            new TabId(1),
            new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board"),
            2,
            3,
            20) with
        {
            Rows = [parent],
        };

        return AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work) with { Tabs = [tab], Active = tab.Id },
        };
    }
}
