using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Forms;

[Trait("Os", "Independent")]
public class CreateFormReducerTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueTypeSummary _task = new("10", "Task", 0, false);
    private static readonly IssueTypeSummary _epic = new("11", "Epic", 1, false);
    private static readonly IssueTypeSummary _subtask = new("12", "Sub-task", -1, true);

    private static readonly CreateIssueType[] _types =
    [
        new("10", "Task", string.Empty, 0, false),
        new("11", "Epic", string.Empty, 1, false),
        new("12", "Sub-task", string.Empty, -1, true),
        new("13", "Bug", string.Empty, 0, false),
    ];

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenATaskIsSelected_WhenCreateIsRun_ThenTheProjectComesFromItAndNoParentIsSet()
    {
        var result = Palette(Tabbed(_task), "create");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        var target = Assert.IsType<CreateFormTarget>(effect.Target);
        Assert.Equal("A", target.ProjectKey);
        Assert.Null(target.ParentKey);
        Assert.False(target.SubtasksOnly);
        Assert.Null(target.IssueType);
        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal("https://work.atlassian.net/browse/A", result.State.Session.Tabs[0].Form!.BrowserUrl);
    }

    [Fact]
    public void GivenAnEpicIsSelected_WhenCreateIsRun_ThenTheEpicIsTheParentOfTheNewIssue()
    {
        var result = Palette(Tabbed(_epic), "create");

        var target = Assert.IsType<CreateFormTarget>(Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects)).Target);
        Assert.Equal("A-1", target.ParentKey);
        Assert.False(target.SubtasksOnly);
        Assert.Equal("Create issue in A under A-1", target.Title);
    }

    [Fact]
    public void GivenATaskIsSelected_WhenTheSubtaskKeysAreUsed_ThenTheTaskIsTheRequiredParent()
    {
        var state = Press(Press(Press(Tabbed(_task), "Space").State, "i").State, "C");

        var target = Assert.IsType<CreateFormTarget>(Assert.IsType<LoadFormMetadataEffect>(Assert.Single(state.Effects)).Target);
        Assert.Equal("A-1", target.ParentKey);
        Assert.True(target.SubtasksOnly);
        Assert.Equal("Create sub-task of A-1", target.Title);
    }

    [Theory]
    [InlineData(true, "A sub-task cannot have sub-tasks")]
    [InlineData(false, "An epic has no sub-tasks")]
    public void GivenAnIssueThatCannotHaveSubtasks_WhenCreateSubtaskIsRun_ThenNothingOpens(bool isSubtask, string text)
    {
        var result = Palette(Tabbed(isSubtask ? _subtask : _epic), "create subtask");

        Assert.Empty(result.Effects);
        Assert.Null(result.State.Session.Tabs[0].Form);
        Assert.StartsWith(text, result.State.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNothingIsSelected_WhenCreateIsRun_ThenTheStatusLineSaysWhereTheProjectComesFrom()
    {
        var state = AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = SiteSession.Empty(_work) };

        var result = Palette(state, "create");

        Assert.Empty(result.Effects);
        Assert.Contains("selected or shown issue", result.State.Message);
    }

    [Fact]
    public void GivenTheTypesArrive_WhenCreateOpenedPlain_ThenOnlyNonSubtaskTypesAreOffered()
    {
        var state = TypesShown(_task, false);

        var form = state.Session.Tabs[0].Form!;
        Assert.True(form.IsChoosingType);
        Assert.Equal(["Task", "Epic", "Bug"], form.TypeChoices!.Select(type => type.Name));
    }

    [Fact]
    public void GivenTheTypesArrive_WhenCreateOpenedForASubtask_ThenOnlySubtaskTypesAreOffered()
    {
        var state = TypesShown(_task, true);

        Assert.Equal(["Sub-task"], state.Session.Tabs[0].Form!.TypeChoices!.Select(type => type.Name));
    }

    [Fact]
    public void GivenAProjectWithoutSubtaskTypes_WhenTheTypesArrive_ThenTheFormSaysSo()
    {
        var opened = Palette(Tabbed(_task), "create subtask");
        var tab = opened.State.Session.Tabs[0];
        var types = new[] { _types[0] };

        var state = _reducer.Reduce(opened.State, new FormTypesLoaded(tab.Id, tab.Form!.RequestId, types)).State;

        var form = state.Session.Tabs[0].Form!;
        Assert.False(form.IsChoosingType);
        Assert.Contains("no issue types", form.TopError);
    }

    [Fact]
    public void GivenTheTypePicker_WhenClickingARow_ThenTheFieldsOfThatTypeAreRequestedLikeEnterOnIt()
    {
        var state = TypesShown(_task, false);

        var chosen = _reducer.Reduce(state, Click(MouseTarget.FormChoice, 1));

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(chosen.Effects));
        var target = Assert.IsType<CreateFormTarget>(effect.Target);
        Assert.Equal("Epic", target.IssueType!.Name);
        Assert.False(chosen.State.Session.Tabs[0].Form!.IsChoosingType);
    }

    [Fact]
    public void GivenTheTypePicker_WhenClickingARowThatDoesNotExist_ThenNothingChanges()
    {
        var state = TypesShown(_task, false);

        var result = _reducer.Reduce(state, Click(MouseTarget.FormChoice, 9));

        Assert.Empty(result.Effects);
        Assert.Equal(state, result.State);
    }

    [Fact]
    public void GivenAFormThatSavesOrAsksToDiscard_WhenClickingAField_ThenNothingChanges()
    {
        var loaded = ChosenForm(_task, false, SummaryField(), DescriptionField());
        var form = loaded.Session.Tabs[0].Form!;
        var saving = form with { OperationId = 7 };
        var discarding = form with { DiscardPending = true };

        foreach (var shown in new[] { saving, discarding })
        {
            var state = WithForm(loaded, shown);

            var result = _reducer.Reduce(state, Click(MouseTarget.FormField, 1));

            Assert.Empty(result.Effects);
            Assert.Equal(0, result.State.Session.Tabs[0].Form!.Focus);
        }
    }

    [Fact]
    public void GivenAForm_WhenClickingAnEditableFieldOrANonEditableOne_ThenOnlyTheEditableOneTakesTheFocus()
    {
        var loaded = ChosenForm(_task, false, SummaryField(), DescriptionField());
        var form = loaded.Session.Tabs[0].Form!;
        var fields = new List<FormField>(form.Fields) { form.Fields[0] with { Kind = FormFieldKind.Unsupported } };
        var shown = WithForm(loaded, form with { Fields = fields });

        var focused = _reducer.Reduce(shown, Click(MouseTarget.FormField, 1)).State.Session.Tabs[0].Form!;
        var ignored = _reducer.Reduce(shown, Click(MouseTarget.FormField, 2)).State.Session.Tabs[0].Form!;

        Assert.Equal(1, focused.Focus);
        Assert.Equal(0, ignored.Focus);
    }

    [Fact]
    public void GivenTheTypePicker_WhenMovingAndChoosing_ThenTheFieldsOfThatTypeAreRequested()
    {
        var state = TypesShown(_task, false);

        state = Press(state, "Down").State;
        state = Press(state, "Down").State;
        state = Press(state, "Up").State;
        var chosen = Press(state, "Enter");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(chosen.Effects));
        var target = Assert.IsType<CreateFormTarget>(effect.Target);
        var form = chosen.State.Session.Tabs[0].Form!;
        Assert.Equal("Epic", target.IssueType!.Name);
        Assert.True(form.IsLoading);
        Assert.False(form.IsChoosingType);
        Assert.Equal(effect.RequestId, form.RequestId);
        Assert.Equal("Create Epic in A", form.Target.Title);
    }

    [Fact]
    public void GivenTheTypePicker_WhenMovingPastTheEnds_ThenTheCursorStaysInside()
    {
        var state = Press(TypesShown(_task, false), "Up").State;

        Assert.Equal(0, state.Session.Tabs[0].Form!.TypeCursor);

        for (var step = 0; step < 6; step++)
        {
            state = Press(state, "Down").State;
        }

        Assert.Equal(2, state.Session.Tabs[0].Form!.TypeCursor);
    }

    [Fact]
    public void GivenTheTypePicker_WhenEscIsPressed_ThenTheFormClosesAndNothingIsSent()
    {
        var closed = Press(TypesShown(_task, false), "Esc");

        Assert.Null(closed.State.Session.Tabs[0].Form);
        Assert.Equal(InputMode.Normal, closed.State.Mode);
        Assert.Empty(closed.Effects);
    }

    [Fact]
    public void GivenATypeWithoutAParentField_WhenFieldsLoadForAnEpicChild_ThenTheParentIsDropped()
    {
        var opened = Palette(Tabbed(_epic), "create");

        var state = Loaded(opened.State, SummaryField(), DescriptionField());

        var form = state.Session.Tabs[0].Form!;
        Assert.Null(((CreateFormTarget)form.Target).ParentKey);
    }

    [Fact]
    public void GivenATypeWithAParentField_WhenFieldsLoadForAnEpicChild_ThenTheParentIsPrefilledAndSearchable()
    {
        var opened = Palette(Tabbed(_epic), "create");

        var state = Loaded(opened.State, [.. FormFieldMapper.FromCreate(CreateFields(false), "A-1")]);

        var form = state.Session.Tabs[0].Form!;
        var parent = form.Fields.Single(field => field.Id == FormFieldIds.Parent);
        Assert.Equal("A-1", ((CreateFormTarget)form.Target).ParentKey);
        Assert.Equal(["A-1"], parent.Value.Chosen);
        Assert.True(parent.IsEditable);
        Assert.False(form.IsDirty);
    }

    [Fact]
    public void GivenATypeThatRequiresAParent_WhenNoParentWasGiven_ThenTheFieldSaysRequiredAndNothingIsSent()
    {
        var state = Loaded(Palette(Tabbed(_task), "create").State, SummaryField(), ParentField(true));
        state = Type(state, "Title");

        var submitted = Press(state, "Ctrl-s", _start);

        Assert.Empty(submitted.Effects);
        Assert.NotNull(submitted.State.Session.Tabs[0].Form!.Fields.Single(field => field.Id == FormFieldIds.Parent).Error);
    }

    [Fact]
    public void GivenNoSummary_WhenSubmitting_ThenTheFieldSaysRequiredAndNothingIsSent()
    {
        var state = Loaded(Palette(Tabbed(_task), "create").State, SummaryField(), DescriptionField());

        var submitted = Press(state, "Ctrl-s", _start);

        var form = submitted.State.Session.Tabs[0].Form!;
        Assert.Empty(submitted.Effects);
        Assert.Equal(FormValidation.RequiredText, form.Fields[0].Error);
        Assert.Empty(submitted.State.Session.Operations);
    }

    [Fact]
    public void GivenASummary_WhenSubmitting_ThenACreateRequestWithTheChosenTypeAndParentIsStarted()
    {
        var state = ChosenForm(_epic, false, SummaryField(), DescriptionField(), ParentField(false));
        state = Type(state, "Add login");

        var submitted = Press(state, "Ctrl-s", _start);

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects));
        var request = Assert.IsType<CreateRequest>(effect.Request);
        var operation = Assert.Single(submitted.State.Session.Operations);
        Assert.Equal("A", request.ProjectKey);
        Assert.Equal("10", request.IssueTypeId);
        Assert.Equal("A-1", request.ParentKey);
        Assert.Equal("Add login", request.Summary);
        Assert.Single(request.Changes);
        Assert.Equal(OperationKind.CreateIssue, operation.Kind);
        Assert.Equal(operation.Id, submitted.State.Session.Tabs[0].Form!.OperationId);
        Assert.Equal("Creating Task in A", submitted.State.Message);
    }

    [Fact]
    public void GivenAFilledForm_WhenEscThenNIsPressed_ThenTheFormStaysAndNothingIsSent()
    {
        var state = Type(ChosenForm(_task, false, SummaryField()), "Draft");

        var asked = Press(state, "Esc");
        var kept = Press(asked.State, "n");

        Assert.True(asked.State.Session.Tabs[0].Form!.DiscardPending);
        Assert.False(kept.State.Session.Tabs[0].Form!.DiscardPending);
        Assert.Empty(kept.Effects);
        Assert.Empty(kept.State.Session.Operations);
    }

    [Fact]
    public void GivenAFilledForm_WhenEscThenYIsPressed_ThenTheFormClosesAndNothingWasSent()
    {
        var state = Type(ChosenForm(_task, false, SummaryField()), "Draft");

        var asked = Press(state, "Esc");
        var discarded = Press(asked.State, "y");

        Assert.Null(discarded.State.Session.Tabs[0].Form);
        Assert.Empty(discarded.Effects);
        Assert.Empty(discarded.State.Session.Operations);
        Assert.Equal(InputMode.Normal, discarded.State.Mode);
    }

    [Fact]
    public void GivenJiraRefusesTheParent_WhenTheCreateFails_ThenTheFormUnlocksWithTheMessageAtTheField()
    {
        var sent = Press(Type(ChosenForm(_task, false, SummaryField(), ParentField(false)), "Title"), "Ctrl-s", _start);
        var id = sent.State.Session.Operations[0].Id;
        var error = new WriteError(
            WriteErrorKind.Validation,
            400,
            "The issue is not valid.",
            [new WriteFieldError("parent", "Given parent issue is not valid for this issue type.")],
            []);

        var failed = _reducer.Reduce(sent.State, new WriteFailed(id, _start, error)).State;

        var form = failed.Session.Tabs[0].Form!;
        Assert.Null(form.OperationId);
        Assert.Equal("Given parent issue is not valid for this issue type.", form.Fields.Single(f => f.Id == "parent").Error);
        Assert.Empty(failed.Session.Operations);
    }

    [Fact]
    public void GivenAnAcceptedCreate_WhenJiraConfirms_ThenTheFormClosesTheViewReloadsAndTheKeyIsSaid()
    {
        var sent = Press(Type(ChosenForm(_task, false, SummaryField()), "Title"), "Ctrl-s", _start);
        var id = sent.State.Session.Operations[0].Id;

        var done = _reducer.Reduce(sent.State, new WriteSucceeded(id, _start, [], CreatedKey: "A-7"));

        Assert.Null(done.State.Session.Tabs[0].Form);
        Assert.Equal("Created A-7", done.State.Message);
        Assert.Contains(done.Effects, effect => effect is InvalidateCacheEffect);
        Assert.Contains(done.Effects, effect => effect is LoadViewPageEffect);
        Assert.Empty(done.State.Session.Tabs[0].Detail);
        Assert.Empty(done.State.Session.Operations);
    }

    [Fact]
    public void GivenOpenAfterIsOn_WhenJiraConfirms_ThenTheNewIssueOpensInTheDetail()
    {
        var state = ChosenForm(_task, false, SummaryField());
        state = Press(state, "Ctrl-g").State;
        var sent = Press(Type(state, "Title"), "Ctrl-s", _start);
        var id = sent.State.Session.Operations[0].Id;

        var done = _reducer.Reduce(sent.State, new WriteSucceeded(id, _start, [], CreatedKey: "A-7"));

        var detail = Assert.Single(done.State.Session.Tabs[0].Detail);
        Assert.Equal("A-7", detail.Key);
        Assert.Contains(done.Effects, effect => effect is LoadIssueDetailEffect);
    }

    [Fact]
    public void GivenTheFormToggle_WhenPressedTwice_ThenOpenAfterIsOffAgain()
    {
        var state = ChosenForm(_task, false, SummaryField());

        var on = Press(state, "Ctrl-g").State;
        var off = Press(on, "Ctrl-g").State;

        Assert.True(((CreateFormTarget)on.Session.Tabs[0].Form!.Target).OpenAfter);
        Assert.False(((CreateFormTarget)off.Session.Tabs[0].Form!.Target).OpenAfter);
    }

    [Fact]
    public void GivenALostReply_WhenTheCheckFindsTheIssue_ThenItIsAKnownSuccess()
    {
        var sent = Press(Type(ChosenForm(_task, false, SummaryField()), "Title"), "Ctrl-s", _start);
        var id = sent.State.Session.Operations[0].Id;
        var lost = new WriteError(WriteErrorKind.UnknownOutcome, null, "timeout", [], []);

        var unknown = _reducer.Reduce(sent.State, new WriteOutcomeUnknown(id, _start, lost));
        var found = _reducer.Reduce(
            unknown.State,
            new WriteVerified(id, _start, VerifyOutcome.Applied, [Issue(_task) with { Key = "A-9" }]));

        Assert.Contains(unknown.Effects, effect => effect is VerifyWriteEffect);
        Assert.Null(found.State.Session.Tabs[0].Form);
        Assert.Equal("Created A-9", found.State.Message);
    }

    [Fact]
    public void GivenALostReply_WhenTheCheckCannotDecide_ThenTheMessageWarnsAboutADuplicateAndTheFormUnlocks()
    {
        var sent = Press(Type(ChosenForm(_task, false, SummaryField()), "Title"), "Ctrl-s", _start);
        var id = sent.State.Session.Operations[0].Id;
        var lost = new WriteError(WriteErrorKind.UnknownOutcome, null, "timeout", [], []);

        var unknown = _reducer.Reduce(sent.State, new WriteOutcomeUnknown(id, _start, lost));
        var unclear = _reducer.Reduce(unknown.State, new WriteVerified(id, _start, VerifyOutcome.Unreachable, []));

        Assert.Contains("may or may not have been created", unclear.State.Message);
        Assert.Null(unclear.State.Session.Tabs[0].Form!.OperationId);
        Assert.Empty(unclear.State.Session.Operations);
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenLookingUpTheCreateKeys_ThenTheyAreBoundWithTitles()
    {
        var keymap = DefaultKeymap.Load();

        Assert.Equal("Space i c", keymap.FindKeys(InputMode.Normal, ActionIds.IssueCreate));
        Assert.Equal("Space i C", keymap.FindKeys(InputMode.Normal, ActionIds.IssueCreateSubtask));
        Assert.Equal("Ctrl-g", keymap.FindKeys(InputMode.Insert, ActionIds.FormOpenAfter));
        Assert.NotNull(ActionCatalog.FindTitle(ActionIds.IssueCreate));
        Assert.NotNull(ActionCatalog.FindTitle(ActionIds.IssueCreateSubtask));
    }

    [Fact]
    public void GivenCreateMetadata_WhenMapped_ThenADateTimeFieldIsADateTimeAndTheParentCarriesTheGivenKey()
    {
        CreateField[] fields =
        [
            new("summary", "Summary", true, false, CreateFieldKind.Text, [], null),
            new("customfield_1", "Start", false, false, CreateFieldKind.DateTime, [], null),
            new("duedate", "Due", false, false, CreateFieldKind.Date, [], null),
            new("parent", "Parent", false, false, CreateFieldKind.Parent, [], null),
        ];

        var mapped = FormFieldMapper.FromCreate(fields, "A-1");

        Assert.Equal(FormFieldKind.DateTime, mapped.Single(field => field.Id == "customfield_1").Kind);
        Assert.Equal(FormFieldKind.Date, mapped.Single(field => field.Id == "duedate").Kind);
        Assert.Equal(["A-1"], mapped.Single(field => field.Id == "parent").Value.Chosen);
    }

    private static AppState WithForm(AppState state, FormState form)
    {
        var tab = state.Session.Tabs[0] with { Form = form };

        return state with { Session = state.Session with { Tabs = [tab] } };
    }

    private static OverlayRowClicked Click(MouseTarget target, int index)
    {
        return new OverlayRowClicked(target, index, ActionIds.FormNext, _start);
    }

    private AppState TypesShown(IssueTypeSummary selected, bool subtask)
    {
        var opened = Palette(Tabbed(selected), subtask ? "create subtask" : "create");
        var tab = opened.State.Session.Tabs[0];

        return _reducer.Reduce(opened.State, new FormTypesLoaded(tab.Id, tab.Form!.RequestId, _types)).State;
    }

    private AppState ChosenForm(IssueTypeSummary selected, bool subtask, params FormField[] fields)
    {
        var chosen = Press(TypesShown(selected, subtask), "Enter").State;

        return Loaded(chosen, fields);
    }

    private AppState Loaded(AppState state, params FormField[] fields)
    {
        var tab = state.Session.Tabs[0];

        return _reducer.Reduce(state, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, fields)).State;
    }

    private AppState Type(AppState state, string text)
    {
        return text.Aggregate(state, (current, character) => Press(current, character.ToString()).State);
    }

    private ReduceResult Press(AppState state, string key, DateTimeOffset at = default)
    {
        return _reducer.Reduce(state, new KeyPressed(key == " " ? "Space" : key, at));
    }

    private ReduceResult Palette(AppState state, string command)
    {
        var current = state with { Mode = InputMode.Command, PaletteText = command };

        return _reducer.Reduce(current, new KeyPressed("Enter"));
    }

    private static CreateField[] CreateFields(bool parentRequired)
    {
        return
        [
            new("summary", "Summary", true, false, CreateFieldKind.Text, [], null),
            new("parent", "Parent", parentRequired, false, CreateFieldKind.Parent, [], null),
        ];
    }

    private static FormField SummaryField()
    {
        return FormField.Create(FormFieldIds.Summary, "Summary", FormFieldKind.Text, true, []);
    }

    private static FormField DescriptionField()
    {
        return FormField.Create(FormFieldIds.Description, "Description", FormFieldKind.Paragraph, false, []);
    }

    private static FormField ParentField(bool required)
    {
        return FormField.Create(FormFieldIds.Parent, "Parent", FormFieldKind.Parent, required, []);
    }

    private AppState Tabbed(IssueTypeSummary type)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me", "acc-me") },
        };
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0];
        var page = new IssuePage([Issue(type)], null, null);

        return _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _start)).State;
    }

    private static IssueSummary Issue(IssueTypeSummary type)
    {
        return new IssueSummary(
            "1",
            "A-1",
            "Issue 1",
            new IssueStatus("1", "To Do", "new"),
            type,
            null,
            null,
            [],
            null,
            null,
            "A");
    }
}
