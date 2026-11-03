using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Tests.Sprints;

[Trait("Os", "Independent")]
public class SprintGoalNoticeTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenASprintWithAGoal_WhenPressingSpaceSG_ThenTheNoticeOpensWithNameAndFullGoal()
    {
        var state = Press(Press(Press(OnSprint("Ship the board\nand the backlog"), "Space"), "s"), "g");

        Assert.Equal(InputMode.Confirm, state.Mode);
        Assert.Equal(SprintGoalNotice.Title, state.Confirm!.Title);
        Assert.Equal("Sprint 4\n\nShip the board\nand the backlog", state.Confirm.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void GivenNoGoal_WhenOpening_ThenOnlyTheNoteShows(string? goal)
    {
        var state = SprintGoalNotice.Open(OnSprint(goal));

        Assert.Equal(InputMode.Normal, state.Mode);
        Assert.Null(state.Confirm);
        Assert.Equal(SprintGoalNotice.NoGoalText, state.Message);
    }

    [Fact]
    public void GivenATabWithoutSprint_WhenOpening_ThenTheNoteSaysSo()
    {
        var state = SprintGoalNotice.Open(Open(null));

        Assert.Equal(SprintGoalNotice.NoSprintText, state.Message);
    }

    [Theory]
    [InlineData("y")]
    [InlineData("n")]
    [InlineData("Esc")]
    public void GivenTheNotice_WhenAnsweringAnyKey_ThenItClosesWithoutAMessage(string key)
    {
        var open = SprintGoalNotice.Open(OnSprint("Goal"));

        var state = Press(open, key);

        Assert.Equal(InputMode.Normal, state.Mode);
        Assert.Null(state.Confirm);
        Assert.Null(state.Message);
    }

    [Fact]
    public void GivenTheNotice_WhenLaidOut_ThenWrapsKeepsParagraphsAndHasOnlyTheCloseRow()
    {
        var request = SprintGoalNotice.Open(OnSprint("one two three four five six seven eight nine ten eleven")).Confirm!;

        var layout = ConfirmDialog.Layout(request, 30, 30)!;

        Assert.True(layout.Notice);
        Assert.Equal(-1, layout.NoRow);
        Assert.Equal("Sprint 4", layout.Question[0]);
        Assert.Equal(string.Empty, layout.Question[1]);
        Assert.True(layout.Question.Count > 3);
        Assert.Empty(layout.Warning);
        Assert.Null(ConfirmDialog.Layout(request, 30, 5));
    }

    [Fact]
    public void GivenSprintViews_WhenBuildingTheViewMenu_ThenTheGoalRowShowsOnlyWithAGoal()
    {
        Assert.Contains(ActionIds.SprintGoal, Actions(OnSprint("Goal")));
        Assert.DoesNotContain(ActionIds.SprintGoal, Actions(OnSprint(null)));
    }

    private static IEnumerable<string> Actions(AppState state)
    {
        return ContextMenus.ForView(state)!.Items.Select(item => item.Action);
    }

    private AppState Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key)).State;
    }

    private AppState OnSprint(string? goal)
    {
        return Open(new Sprint(4, "Sprint 4", SprintState.Active, goal, null, null, null, 7));
    }

    private AppState Open(Sprint? sprint)
    {
        var start = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };
        var definition = new ViewDefinition(ViewSourceKind.Sprint, "7:4", ViewLayout.Columns, "Sprint 4");
        var opened = _reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0] with { Sprint = sprint, SprintResolved = true };

        return opened with { Session = opened.Session with { Tabs = [tab] } };
    }
}
