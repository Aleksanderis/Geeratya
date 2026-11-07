using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>
/// The Details pane as a sidebar with dropdowns (#704): focus between the columns, the fields as filled blocks, and
/// the dropdown of Priority, Assignee and Labels through the real reducer, the form and the write path.
/// </summary>
[Trait("Os", "Independent")]
public class DetailSidebarTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly FieldOption[] _priorities = [new("1", "Highest"), new("2", "High"), new("3", "Medium")];
    private static readonly string[] _allEditable =
        [DetailFieldIds.Priority, DetailFieldIds.Assignee, DetailFieldIds.Labels];

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenWideLayout_WhenPressingCtrlLAndCtrlH_ThenTheFocusMovesBetweenTheColumns()
    {
        var state = Opened(120);

        var side = Press(state, "Ctrl-l").State;
        var back = Press(side, "Ctrl-h").State;

        Assert.Null(Top(state).SideCursor);
        Assert.NotNull(Top(side).SideCursor);
        Assert.Equal(Top(state).Cursor, Top(side).Cursor);
        Assert.Null(Top(back).SideCursor);
        Assert.Equal(Top(state).Cursor, Top(back).Cursor);
    }

    [Fact]
    public void GivenFocusOnTheSidebar_WhenMovingByJAndK_ThenTheCursorStepsFromFieldToField()
    {
        var side = Press(Opened(120), "Ctrl-l").State;

        Assert.Equal(DetailFieldIds.Status, SideTarget(side));
        Assert.Equal(DetailFieldIds.Priority, SideTarget(Press(side, "j").State));
        Assert.Equal(DetailFieldIds.Status, SideTarget(Press(Press(side, "j").State, "k").State));
        Assert.Equal(Top(side).Cursor, Top(Press(side, "j").State).Cursor);
    }

    [Fact]
    public void GivenFocusOnTheSidebar_WhenGoingToTheBottom_ThenTheLastFieldIsFocused()
    {
        var side = Press(Opened(120), "Ctrl-l").State;

        var last = Press(side, "G").State;

        Assert.Equal(DetailFieldIds.Labels, SideTarget(last));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(80)]
    public void GivenNarrowLayout_WhenPressingCtrlL_ThenThereIsNoSidebarToFocus(int columns)
    {
        var state = Opened(columns);

        var next = Press(state, "Ctrl-l").State;

        Assert.Null(Top(next).Document!.Side);
        Assert.Null(Top(next).SideCursor);
    }

    [Fact]
    public void GivenFocusOnTheSidebar_WhenPressingEnterOnStatus_ThenTheMoveToPickerLoads()
    {
        var side = Press(Opened(120), "Ctrl-l").State;

        var result = Press(side, "Enter");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.IsType<TransitionPickTarget>(effect.Target);
        Assert.Null(Active(result.State).Form!.Dropdown);
    }

    [Theory]
    [InlineData(DetailFieldIds.Priority)]
    [InlineData(DetailFieldIds.Labels)]
    public void GivenEditableField_WhenPressingEnter_ThenTheFormOpensAsADropdownUnderTheField(string field)
    {
        var state = OnSideField(Editable(Opened(120)), field);

        var result = Press(state, "Enter");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal(field, Assert.IsType<EditFormTarget>(effect.Target).OnlyId);
        Assert.Equal(field, Active(result.State).Form!.Dropdown);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Fact]
    public void GivenAssigneeField_WhenPressingEnter_ThenThePeoplePickerOpensAsADropdown()
    {
        var state = OnSideField(Editable(Opened(120)), DetailFieldIds.Assignee);

        var result = Press(state, "Enter");

        Assert.Equal("Assign A-1", Active(result.State).Form!.Target.Title);
        Assert.Equal(DetailFieldIds.Assignee, Active(result.State).Form!.Dropdown);
    }

    [Fact]
    public void GivenPriorityDropdown_WhenChoosingWithDownAndEnter_ThenTheChoiceIsWrittenThroughTheEditPath()
    {
        var open = PriorityDropdown();

        var moved = Press(Press(Press(open, "Down").State, "Down").State, "Down").State;
        var written = Press(moved, "Enter");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(written.Effects));
        var request = Assert.IsType<EditRequest>(effect.Request);
        var change = Assert.Single(request.Changes);
        Assert.Equal(DetailFieldIds.Priority, change.FieldId);
        Assert.Equal(["2"], change.Value.Chosen);
    }

    [Fact]
    public void GivenPriorityDropdown_WhenChoosingWithCtrlJAndCtrlK_ThenTheSameRowsAreChosenAsWithTheArrows()
    {
        var open = PriorityDropdown();

        var down = Press(Press(Press(open, "Ctrl-j").State, "Ctrl-j").State, "Ctrl-j").State;
        var up = Press(down, "Ctrl-k").State;

        Assert.Equal(2, Active(down).Form!.Suggest!.Cursor);
        Assert.Equal(1, Active(up).Form!.Suggest!.Cursor);
        Assert.Equal("Highest", Active(up).Form!.Suggest!.Selected!.Label);
    }

    [Fact]
    public void GivenPriorityDropdown_WhenPressingEsc_ThenItClosesWithoutWritingAndTheDetailKeepsTheFocus()
    {
        var open = Press(PriorityDropdown(), "Down").State;

        var closed = Press(open, "Esc");

        Assert.Null(Active(closed.State).Form);
        Assert.Empty(closed.Effects);
        Assert.Equal(InputMode.Normal, closed.State.Mode);
        Assert.Equal(DetailFieldIds.Priority, SideTarget(closed.State));
    }

    [Fact]
    public void GivenAssigneeDropdown_WhenChoosingUnassigned_ThenTheAssigneeIsWrittenAtOnce()
    {
        var opened = Press(OnSideField(Editable(Opened(120)), DetailFieldIds.Assignee), "Enter").State;

        var chosen = Press(Press(opened, "Down").State, "Enter");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(chosen.Effects));
        var request = Assert.IsType<AssignRequest>(effect.Request);
        Assert.Equal("A-1", request.Key);
        Assert.Null(request.Assignee);
    }

    [Fact]
    public void GivenAssigneeDropdown_WhenPressingEsc_ThenItClosesWithoutWriting()
    {
        var opened = Press(OnSideField(Editable(Opened(120)), DetailFieldIds.Assignee), "Enter").State;

        var closed = Press(opened, "Esc");

        Assert.Null(Active(closed.State).Form);
        Assert.Empty(closed.Effects);
    }

    [Fact]
    public void GivenLabelsDropdown_WhenTypingALabelAndConfirming_ThenTheLabelsAreWrittenThroughTheEditPath()
    {
        var open = LabelsDropdown();

        var typed = Press(Press(open, "u").State, "i").State;
        var written = Press(typed, "Enter");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(written.Effects));
        var request = Assert.IsType<EditRequest>(effect.Request);
        Assert.Equal(["backend", "ui"], Assert.Single(request.Changes).Value.Chosen);
    }

    [Fact]
    public void GivenLabelsDropdown_WhenPressingEsc_ThenItClosesWithoutWriting()
    {
        var closed = Press(LabelsDropdown(), "Esc");

        Assert.Null(Active(closed.State).Form);
        Assert.Empty(closed.Effects);
    }

    [Fact]
    public void GivenEditMetadataWithoutPriority_WhenPressingEnterOnPriority_ThenNothingOpensAndTheStatusLineSaysSo()
    {
        var state = OnSideField(Editable(Opened(120), DetailFieldIds.Labels), DetailFieldIds.Priority);

        var result = Press(state, "Enter");

        Assert.Equal("Priority cannot be edited on this issue.", result.State.Message);
        Assert.Empty(result.Effects);
        Assert.Null(Active(result.State).Form);
    }

    [Fact]
    public void GivenEditMetadataWithNoEditableFields_WhenBuilding_ThenOnlyStatusCarriesTheDropdownMark()
    {
        var opened = Opened(120);
        var state = _reducer.Reduce(opened, new IssueEditableLoaded(Active(opened).Id, "A-1", [])).State;

        var side = Top(state).Document!.Side!;

        Assert.Single(side.Lines, line => line.PlainText.Contains('▾'));
        Assert.Contains("● To Do ▾", side.Lines.Single(line => line.PlainText.Contains('▾')).PlainText);
    }

    [Fact]
    public void GivenTheCursorOnTheEstimate_WhenTheEditMetadataAddsTheEmptyEpicRow_ThenTheCursorStaysOnTheEstimate()
    {
        var state = OnSideField(Opened(120, estimate: 3), DetailFieldIds.Estimate);
        var before = Top(state).SideCursor;

        var loaded = Editable(state, "parent");

        Assert.NotEqual(before, Top(loaded).SideCursor);
        Assert.Equal(DetailFieldIds.Estimate, SideTarget(loaded));
    }

    [Fact]
    public void GivenEditMetadataArrives_WhenTheIssueIsOpen_ThenTheFieldsAreMarkedEditableWithoutMovingTheCursor()
    {
        var state = Press(Opened(120), "Ctrl-l").State;
        var before = Top(state);

        var marked = Editable(state);

        Assert.Equal(before.SideCursor, Top(marked).SideCursor);
        Assert.True(Top(marked).Document!.Side!.Lines.Count(line => line.PlainText.Contains('▾')) >= 4);
    }

    [Fact]
    public void GivenAClickOnASidebarField_WhenPlacingTheCursor_ThenTheSidebarTakesTheFocusOnThatField()
    {
        var state = Opened(120, bodyHeight: 28);
        var box = DetailOverlayGeometry.Place(120, 28);
        var column = box.Left + DetailLayout.MainWidth(box.Width) + DetailLayout.SidebarGap + 3;
        var priorityRow = Top(state).Document!.Side!.Targets.ToList().FindIndex(
            target => target is { Kind: DetailTargetKind.Field, Value: DetailFieldIds.Priority });

        var placed = ViewReducer.PlaceCursor(state, new PointerPlaced(column, box.Top + priorityRow, 120));

        Assert.NotNull(placed);
        Assert.Equal(DetailFieldIds.Priority, SideTarget(placed.State));
    }

    [Fact]
    public void GivenAClickOnTheMainColumn_WhenTheSidebarHasTheFocus_ThenTheMainColumnTakesTheFocusBack()
    {
        var state = Press(Opened(120, bodyHeight: 28), "Ctrl-l").State;
        var box = DetailOverlayGeometry.Place(120, 28);

        var placed = ViewReducer.PlaceCursor(state, new PointerPlaced(box.Left + 3, box.Top + 1, 120));

        Assert.NotNull(placed);
        Assert.Null(Top(placed.State).SideCursor);
    }

    [Fact]
    public void GivenNoSidebarFocus_WhenTheEditMetadataFails_ThenNoEffectIsLeftAndTheIssueStays()
    {
        var state = Opened(120);

        Assert.Null(Top(state).EditableFields);
        Assert.NotNull(Top(state).Issue);
    }

    [Fact]
    public void GivenTheIssueLoads_WhenTheReplyIsApplied_ThenTheEditMetadataIsRequestedOnce()
    {
        var opened = Press(Listed(120, -1), "Enter");
        var detail = Active(opened.State).Detail[0];

        var loaded = _reducer.Reduce(
            opened.State,
            new IssueDetailLoaded(Active(opened.State).Id, detail.IssueRequest, Issue(), _now));

        var effect = Assert.Single(loaded.Effects.OfType<LoadEditableFieldsEffect>());
        Assert.Equal("A-1", effect.Key);
    }

    [Fact]
    public void GivenEditMetadataOfAnotherIssue_WhenItArrives_ThenTheShownIssueIsUnchanged()
    {
        var state = Opened(120);

        var other = _reducer.Reduce(state, new IssueEditableLoaded(Active(state).Id, "A-2", _allEditable)).State;

        Assert.Null(Top(other).EditableFields);
    }

    private AppState PriorityDropdown()
    {
        var opened = Press(OnSideField(Editable(Opened(120)), DetailFieldIds.Priority), "Enter").State;
        var form = Active(opened).Form!;
        var field = FormField.Create(FormFieldIds.Priority, "Priority", FormFieldKind.Select, false, _priorities)
            .WithInitial(FormValue.OfChosen(["3"]));

        return _reducer.Reduce(opened, new FormMetadataLoaded(Active(opened).Id, form.RequestId, [field])).State;
    }

    private AppState LabelsDropdown()
    {
        var opened = Press(OnSideField(Editable(Opened(120)), DetailFieldIds.Labels), "Enter").State;
        var form = Active(opened).Form!;
        var field = FormField.Create(FormFieldIds.Labels, "Labels", FormFieldKind.Labels, false, [])
            .WithInitial(FormValue.OfChosen(["backend"]));

        return _reducer.Reduce(opened, new FormMetadataLoaded(Active(opened).Id, form.RequestId, [field])).State;
    }

    private AppState Editable(AppState state, params string[] fields)
    {
        IReadOnlyList<string> ids = fields.Length == 0 ? _allEditable : fields;

        return _reducer.Reduce(state, new IssueEditableLoaded(Active(state).Id, "A-1", ids)).State;
    }

    private AppState OnSideField(AppState state, string field)
    {
        var side = Top(state).SideCursor is null ? Press(state, "Ctrl-l").State : state;

        for (var step = 0; step < 20 && SideTarget(side) != field; step++)
        {
            side = Press(side, "j").State;
        }

        Assert.Equal(field, SideTarget(side));

        return side;
    }

    private static string? SideTarget(AppState state)
    {
        var detail = Top(state);

        return detail.Document!.Side!.Targets[detail.SideCursor!.Value]?.Value;
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static DetailState Top(AppState state)
    {
        return Active(state).Detail[^1];
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private AppState Opened(int columns, int bodyHeight = -1, double? estimate = null)
    {
        var shown = Press(Listed(columns, bodyHeight), "Enter").State;
        var detail = Active(shown).Detail[0];

        var loaded = new IssueDetailLoaded(
            Active(shown).Id,
            detail.IssueRequest,
            Issue() with { Estimate = estimate },
            _now);

        return _reducer.Reduce(shown, loaded).State;
    }

    private AppState Listed(int columns, int bodyHeight)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var sized = _reducer.Reduce(start, new ViewportChanged(30, columns, bodyHeight)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");

        return _reducer.Reduce(
            opened.State,
            new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([row], null, null), _now)).State;
    }

    private static IssueDetail Issue()
    {
        return new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            _story,
            "Medium",
            new IssueAssignee("acc-eve", "Eve Example"),
            new IssueAssignee("acc-ola", "Ola Nordmann"),
            ["backend"],
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
