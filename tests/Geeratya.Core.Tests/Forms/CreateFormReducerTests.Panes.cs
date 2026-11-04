using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.State;
using Geeratya.Jira.Create.Models;

namespace Geeratya.Core.Tests.Forms;

/// <summary>The create form on the three-pane layout (#708): field order, the two states, pane and field keys, Ctrl-f.</summary>
public partial class CreateFormReducerTests
{
    private const int WideColumns = 120;
    private const int NarrowColumns = 80;
    private const int BodyRows = 30;

    [Fact]
    public void GivenCreateMetadata_WhenMapped_ThenSummaryAndDescriptionLeadAndParentIsFirstOfTheRest()
    {
        CreateField[] fields =
        [
            new("duedate", "Due date", false, false, CreateFieldKind.Date, []),
            new("priority", "Priority", false, false, CreateFieldKind.SingleChoice, []),
            new("description", "Description", false, false, CreateFieldKind.RichText, []),
            new("summary", "Summary", true, false, CreateFieldKind.Text, []),
            new("customfield_9", "Sprint goal", true, false, CreateFieldKind.Text, []),
            new("parent", "Parent", true, false, CreateFieldKind.Parent, []),
            new("customfield_1", "Team", false, false, CreateFieldKind.Unsupported, []),
        ];

        var mapped = FormFieldMapper.FromCreate(fields, "A-1");

        Assert.Equal(
            ["summary", "description", "parent", "customfield_9", "duedate", "priority", "customfield_1"],
            mapped.Select(field => field.Id));
    }

    [Fact]
    public void GivenTheFieldsLoad_WhenTheCreateFormOpens_ThenItIsEditingTheSummary()
    {
        var state = Sized(ChosenForm(_task, false, SummaryField(), DescriptionField()), WideColumns);

        var form = state.Session.Tabs[0].Form!;

        Assert.True(form.Navigable);
        Assert.False(form.Navigating);
        Assert.Equal(0, form.Focus);
        Assert.True(CreatePanes.Applies(form));
    }

    [Fact]
    public void GivenEditing_WhenEscIsPressed_ThenTheFormNavigatesWithTheTextKeptAndEscAgainAsksToDiscard()
    {
        var state = Type(Sized(ChosenForm(_task, false, SummaryField(), DescriptionField()), WideColumns), "Draft");

        var navigating = Press(state, "Esc").State;
        var asked = Press(navigating, "Esc").State;

        Assert.True(navigating.Session.Tabs[0].Form!.Navigating);
        Assert.False(navigating.Session.Tabs[0].Form!.DiscardPending);
        Assert.Equal("Draft", navigating.Session.Tabs[0].Form!.Fields[0].Value.Text);
        Assert.True(asked.Session.Tabs[0].Form!.DiscardPending);
    }

    [Fact]
    public void GivenNavigatingAnUnchangedForm_WhenEscIsPressed_ThenTheFormClosesWithoutAsking()
    {
        var state = Navigating(WideForm());

        var closed = Press(state, "Esc").State;

        Assert.Null(closed.Session.Tabs[0].Form);
    }

    [Fact]
    public void GivenNavigatingTheDetails_WhenPressingJAndK_ThenTheFocusMovesInsideThePaneAndStopsAtItsEnds()
    {
        var state = Focused(Navigating(WideForm()), "parent");

        var down = Press(state, "j").State;
        var up = Press(Press(down, "k").State, "k").State;
        var past = Press(Focused(state, "assignee"), "j").State;

        Assert.Equal("priority", FocusedId(down));
        Assert.Equal("parent", FocusedId(up));
        Assert.Equal("assignee", FocusedId(past));
    }

    [Fact]
    public void GivenNavigatingTheTitle_WhenPressingJ_ThenTheFocusStaysBecauseJMovesInsideAPane()
    {
        var state = Navigating(WideForm());

        Assert.Equal("summary", FocusedId(Press(state, "j").State));
    }

    [Fact]
    public void GivenNavigatingWide_WhenPressingLAndH_ThenTheFocusCrossesToTheNearestFieldOfTheOtherPane()
    {
        var state = Navigating(WideForm());

        var details = Press(state, "l").State;
        var fromDescription = Press(Focused(state, "description"), "l").State;
        var second = Press(Focused(state, "priority"), "h").State;
        var first = Press(Focused(state, "parent"), "h").State;

        Assert.Equal("parent", FocusedId(details));
        Assert.Equal("parent", FocusedId(fromDescription));
        Assert.Equal("description", FocusedId(second));
        Assert.Equal("summary", FocusedId(first));
    }

    [Fact]
    public void GivenNavigatingWide_WhenPressingThePaneKeys_ThenTheFocusMovesBetweenPanes()
    {
        var state = Navigating(WideForm());

        var below = Press(state, "Ctrl-j").State;
        var right = Press(below, "Ctrl-l").State;
        var left = Press(right, "Ctrl-h").State;
        var above = Press(below, "Ctrl-k").State;
        var nowhere = Press(right, "Ctrl-j").State;

        Assert.Equal("description", FocusedId(below));
        Assert.Equal("parent", FocusedId(right));
        Assert.Equal("summary", FocusedId(left));
        Assert.Equal("summary", FocusedId(above));
        Assert.Equal("parent", FocusedId(nowhere));
    }

    [Fact]
    public void GivenNavigatingInOneColumn_WhenPressingThePaneKeys_ThenTheyStepThroughThePanesInReadingOrder()
    {
        var state = Navigating(WideForm(NarrowColumns));

        var description = Press(state, "Ctrl-j").State;
        var details = Press(description, "Ctrl-j").State;
        var back = Press(details, "Ctrl-k").State;
        var sideways = Press(description, "l").State;

        Assert.Equal("description", FocusedId(description));
        Assert.Equal("parent", FocusedId(details));
        Assert.Equal("description", FocusedId(back));
        Assert.Equal("description", FocusedId(sideways));
    }

    [Fact]
    public void GivenNavigating_WhenPressingTabAndShiftTab_ThenTheFocusFollowsTheReadingOrderAndWraps()
    {
        var state = Navigating(WideForm());

        var second = Press(state, "Tab").State;
        var third = Press(second, "Tab").State;
        var wrapped = Press(state, "Shift-Tab").State;

        Assert.Equal("description", FocusedId(second));
        Assert.Equal("parent", FocusedId(third));
        Assert.Equal("assignee", FocusedId(wrapped));
    }

    [Fact]
    public void GivenEditing_WhenPressingCtrlJ_ThenThePaneDoesNotChangeBecauseEditingKeepsTheInsertRows()
    {
        var state = WideForm();

        var after = Press(state, "Ctrl-j").State;

        Assert.Equal("summary", FocusedId(after));
        Assert.False(after.Session.Tabs[0].Form!.Navigating);
    }

    [Fact]
    public void GivenNavigating_WhenPressingEnterOrI_ThenTheFocusedFieldIsEdited()
    {
        var state = Focused(Navigating(WideForm()), "priority");

        var byEnter = Press(state, "Enter").State.Session.Tabs[0].Form!;
        var byLetter = Press(state, "i").State.Session.Tabs[0].Form!;

        Assert.False(byEnter.Navigating);
        Assert.False(byLetter.Navigating);
    }

    [Fact]
    public void GivenNavigating_WhenPressingCtrlG_ThenOpenAfterToggles()
    {
        var state = Navigating(WideForm());

        var on = Press(state, "Ctrl-g").State.Session.Tabs[0].Form!;

        Assert.True(((CreateFormTarget)on.Target).OpenAfter);
        Assert.True(on.Navigating);
    }

    [Fact]
    public void GivenManyOptionalFields_WhenTheFormOpens_ThenOnlyTheFirstFiveDetailsShowAndCtrlFShowsTheRest()
    {
        var state = Navigating(ManyFieldsForm());
        var folded = state.Session.Tabs[0].Form!;

        var more = Press(state, "Ctrl-f").State.Session.Tabs[0].Form!;
        var fewer = Press(Press(state, "Ctrl-f").State, "Ctrl-f").State.Session.Tabs[0].Form!;

        Assert.Equal(["parent", "opt1", "opt2", "opt3", "opt4"], ShownIds(folded));
        Assert.Equal(["opt5", "opt6"], CreatePanes.Folded(folded).Select(index => folded.Fields[index].Id));
        Assert.Equal(7, CreatePanes.Shown(more, PaneIds.Details).Count);
        Assert.Equal(5, CreatePanes.Shown(fewer, PaneIds.Details).Count);
    }

    [Fact]
    public void GivenFoldedFields_WhenTabbingAround_ThenTheFoldedFieldsAreSkipped()
    {
        var state = Focused(Navigating(ManyFieldsForm()), "opt4");

        var next = Press(state, "Tab").State;

        Assert.Equal("summary", FocusedId(next));
    }

    [Fact]
    public void GivenAFocusedFieldThatFoldsAway_WhenCtrlFFoldsAgain_ThenTheFocusMovesToTheLastShownField()
    {
        var state = Press(Navigating(ManyFieldsForm()), "Ctrl-f").State;
        state = Focused(state, "opt6");

        var folded = Press(state, "Ctrl-f").State;

        Assert.Equal("opt4", FocusedId(folded));
    }

    [Fact]
    public void GivenAFoldedFieldWithAnError_WhenCheckingTheFold_ThenItStaysShown()
    {
        var form = Navigating(ManyFieldsForm()).Session.Tabs[0].Form!;
        var index = form.Fields.ToList().FindIndex(field => field.Id == "opt6");
        var fields = form.Fields.ToArray();

        fields[index] = fields[index] with { Error = "Invalid" };

        Assert.True(CreatePanes.IsFolded(form, index));
        Assert.False(CreatePanes.IsFolded(form with { Fields = fields }, index));
    }

    [Fact]
    public void GivenAFewFields_WhenCtrlFIsPressed_ThenNothingChanges()
    {
        var state = Navigating(WideForm());

        var after = Press(state, "Ctrl-f").State;

        Assert.False(after.Session.Tabs[0].Form!.ShowAllFields);
    }

    [Fact]
    public void GivenTheSummaryIsEmpty_WhenCreatingFromTheNavigateState_ThenTheErrorIsAtTheSummaryAndNothingIsSent()
    {
        var state = Navigating(WideForm());

        var submitted = Press(state, "Ctrl-s", _start);

        Assert.Empty(submitted.Effects);
        Assert.Equal(FormValidation.RequiredText, submitted.State.Session.Tabs[0].Form!.Fields[0].Error);
    }

    private AppState WideForm(int columns = WideColumns)
    {
        var state = ChosenForm(
            _task,
            false,
            SummaryField(),
            DescriptionField(),
            ParentField(true),
            FormField.Create("priority", "Priority", FormFieldKind.Select, false, []),
            FormField.Create("assignee", "Assignee", FormFieldKind.User, false, []));

        return Sized(state, columns);
    }

    private AppState ManyFieldsForm()
    {
        var optional = Enumerable.Range(1, 6)
            .Select(number => FormField.Create($"opt{number}", $"Optional {number}", FormFieldKind.Text, false, []));
        FormField[] fields = [SummaryField(), DescriptionField(), ParentField(true), .. optional];

        return Sized(ChosenForm(_task, false, fields), WideColumns);
    }

    private static List<string> ShownIds(FormState form)
    {
        return [.. CreatePanes.Shown(form, PaneIds.Details).Select(index => form.Fields[index].Id)];
    }

    private static AppState Sized(AppState state, int columns)
    {
        return state with { ViewportColumns = columns, ViewportBodyHeight = BodyRows };
    }

    private AppState Navigating(AppState state)
    {
        return Press(state, "Esc").State;
    }

    private static AppState Focused(AppState state, string id)
    {
        var form = state.Session.Tabs[0].Form!;

        return WithForm(state, form with { Focus = form.Fields.ToList().FindIndex(field => field.Id == id) });
    }

    private static string FocusedId(AppState state)
    {
        var form = state.Session.Tabs[0].Form!;

        return form.Fields[form.Focus].Id;
    }
}
