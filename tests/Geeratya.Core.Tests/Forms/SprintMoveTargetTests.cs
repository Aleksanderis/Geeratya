using Geeratya.Core.Forms;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Tests.Forms;

/// <summary>The Sprint field of an issue as a one-field form (the Details dropdown).</summary>
[Trait("Os", "Independent")]
public class SprintMoveTargetTests
{
    private static readonly Sprint _active = new(11, "Sprint 1", SprintState.Active, null, null, null, null, 6);
    private static readonly Sprint _future = new(12, "Sprint 2", SprintState.Future, null, null, null, null, 6);

    [Fact]
    public void GivenSprints_WhenTheFieldIsBuilt_ThenTheBacklogComesFirstAndThenTheSprintsOfTheBoard()
    {
        var target = Target(null);

        var field = target.Field();

        Assert.Equal(FormFieldKind.Select, field.Kind);
        Assert.Equal(["backlog", "11", "12"], field.Options.Select(option => option.Id));
        Assert.Equal(["Backlog", "Sprint 1", "Sprint 2"], field.Options.Select(option => option.Label));
    }

    [Fact]
    public void GivenAnIssueInASprint_WhenTheFieldIsBuilt_ThenThatSprintIsChosenAndNothingIsChanged()
    {
        var field = Target(new IssueSprint(12, "Sprint 2", SprintState.Future)).Field();

        Assert.Equal(["12"], field.Value.Chosen);
        Assert.False(field.IsChanged);
    }

    [Fact]
    public void GivenAnIssueInNoSprint_WhenTheFieldIsBuilt_ThenTheBacklogIsChosenAndNothingIsChanged()
    {
        var field = Target(null).Field();

        Assert.Equal([SprintMoveTarget.BacklogId], field.Value.Chosen);
        Assert.False(field.IsChanged);
    }

    [Fact]
    public void GivenAnIssueInAClosedSprint_WhenTheFieldIsBuilt_ThenTheClosedSprintIsTheCurrentChoiceAndTheBacklogIsAChange()
    {
        var field = Target(new IssueSprint(5, "Sprint 0", SprintState.Closed)).Field();

        Assert.Equal(["backlog", "5", "11", "12"], field.Options.Select(option => option.Id));
        Assert.Equal("Sprint 0 (closed)", field.Options[1].Label);
        Assert.Equal(["5"], field.Value.Chosen);
        Assert.False(field.IsChanged);
        Assert.True((field with { Value = FormValue.OfChosen([SprintMoveTarget.BacklogId]) }).IsChanged);
    }

    [Fact]
    public void GivenAnotherSprintChosen_WhenTheFieldIsChecked_ThenItIsChanged()
    {
        var field = Target(new IssueSprint(11, "Sprint 1", SprintState.Active)).Field();

        var chosen = field with { Value = FormValue.OfChosen(["12"]) };

        Assert.True(chosen.IsChanged);
    }

    [Fact]
    public void GivenASprintChosen_WhenTheRequestIsBuilt_ThenItMovesTheIssueToThatSprint()
    {
        var target = Target(null);

        var request = Assert.IsType<SprintMoveRequest>(target.Request([Change("12")]));

        Assert.Equal(["A-1"], request.Keys);
        Assert.Equal(12, request.Target!.Id);
    }

    [Fact]
    public void GivenTheBacklogChosen_WhenTheRequestIsBuilt_ThenTheIssueLeavesItsSprint()
    {
        var target = Target(new IssueSprint(11, "Sprint 1", SprintState.Active));

        var request = Assert.IsType<SprintMoveRequest>(target.Request([Change(SprintMoveTarget.BacklogId)]));

        Assert.Null(request.Target);
    }

    private static FieldChange Change(string chosen)
    {
        return new FieldChange(SprintMoveTarget.FieldId, FormFieldKind.Select, FormValue.OfChosen([chosen]), FormValue.Empty);
    }

    private static SprintMoveTarget Target(IssueSprint? current)
    {
        return new SprintMoveTarget("A-1", [_active, _future], current, "A-1");
    }
}
