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
    public void GivenExpandedCards_WhenAskingForLoads_ThenOneEffectPerExpandedCard()
    {
        var tab = Start().Session.Tabs[0] with { ExpandedSubtasks = ["A-1", "A-7"] };

        var effects = SubtaskAssigneeReducer.Loads(tab).ToArray();

        Assert.Equal(
            [new LoadSubtaskAssigneesEffect(tab.Id, "A-1"), new LoadSubtaskAssigneesEffect(tab.Id, "A-7")],
            effects);
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
