using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Forms;

[Trait("Os", "Independent")]
public class FormReducerTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenARowWithLabels_WhenLabelsIsRun_ThenAFormOpensInInsertModeAndItsMetadataIsRequested()
    {
        var result = Palette(Tabbed(["backend"]), "labels");

        var tab = result.State.Session.Tabs[0];
        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.True(tab.Form!.IsLoading);
        Assert.Equal(effect.RequestId, tab.Form.RequestId);
        Assert.Equal("A-1", effect.Target.IssueKey);
        Assert.Equal("https://work.atlassian.net/browse/A-1", tab.Form.BrowserUrl);
    }

    [Fact]
    public void GivenNoTab_WhenLabelsIsRun_ThenOnlyTheStatusLineSaysWhy()
    {
        var state = AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work) };

        var result = Palette(state, "labels");

        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Contains("No issue selected", result.State.Message);
    }

    [Fact]
    public void GivenAReplyForAnOldRequest_WhenTheMetadataArrives_ThenTheFormIgnoresIt()
    {
        var opened = Open();
        var tab = opened.Session.Tabs[0];

        var late = _reducer.Reduce(opened, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId - 1, [LabelsField()]));

        Assert.True(late.State.Session.Tabs[0].Form!.IsLoading);
    }

    [Fact]
    public void GivenAFailedMetadataLoad_WhenItArrives_ThenTheFormShowsTheMessageAndEscClosesIt()
    {
        var opened = Open();
        var tab = opened.Session.Tabs[0];

        var failed = _reducer.Reduce(opened, new FormMetadataFailed(tab.Id, tab.Form!.RequestId, "A-1 not found"));
        var closed = Press(failed.State, "Esc");

        Assert.Equal("A-1 not found", failed.State.Session.Tabs[0].Form!.TopError);
        Assert.False(failed.State.Session.Tabs[0].Form!.IsLoading);
        Assert.Null(closed.State.Session.Tabs[0].Form);
        Assert.Equal(InputMode.Normal, closed.State.Mode);
    }

    [Fact]
    public void GivenALoadedForm_WhenTyping_ThenSpaceAddsALabelAndEnterOnAnEmptyBufferSubmits()
    {
        var state = Loaded(["backend"]);

        state = Type(state, "ui team");
        state = Press(state, "Enter").State;
        var submitted = Press(state, "Enter");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects));
        var request = Assert.IsType<EditRequest>(effect.Request);
        Assert.Equal(["backend", "ui", "team"], Assert.Single(request.Changes).Value.Chosen);
        Assert.Equal(FormFieldIds.Labels, request.Changes[0].FieldId);
    }

    [Fact]
    public void GivenASubmittedLabelEdit_WhenItStarts_ThenTheRowShowsTheLabelsAndTheFormShowsSaving()
    {
        var state = Type(Loaded(["backend"]), "new");

        var submitted = Press(state, "Ctrl-s", _start);

        var tab = submitted.State.Session.Tabs[0];
        var operation = Assert.Single(submitted.State.Session.Operations);
        Assert.Equal(["backend", "new"], tab.Rows[0].Labels);
        Assert.Equal(operation.Id, tab.Form!.OperationId);
        Assert.Equal(OperationKind.EditFields, operation.Kind);
        Assert.Equal(_start, operation.StartedAt);
    }

    [Fact]
    public void GivenASavingForm_WhenKeysAreTyped_ThenNothingChangesAndEscClosesItWhileTheWriteRuns()
    {
        var saving = Press(Type(Loaded([]), "x"), "Ctrl-s", _start).State;

        var typed = Press(saving, "a");
        var closed = Press(saving, "Esc");

        Assert.Same(saving, typed.State);
        Assert.Null(closed.State.Session.Tabs[0].Form);
        Assert.Single(closed.State.Session.Operations);
        Assert.Equal(InputMode.Normal, closed.State.Mode);
    }

    [Fact]
    public void GivenASavingForm_WhenJiraConfirms_ThenTheFormClosesAndTheModeIsNormal()
    {
        var saving = Press(Type(Loaded([]), "x"), "Ctrl-s", _start).State;
        var id = saving.Session.Operations[0].Id;

        var done = _reducer.Reduce(saving, new WriteSucceeded(id, _start, []));

        Assert.Null(done.State.Session.Tabs[0].Form);
        Assert.Equal(InputMode.Normal, done.State.Mode);
        Assert.Equal("Saved A-1", done.State.Message);
    }

    [Fact]
    public void GivenASavingForm_WhenJiraNamesTheLabelsField_ThenTheFormUnlocksWithTheMessageAtTheFieldAndTheRowIsBack()
    {
        var saving = Press(Type(Loaded(["backend"]), "bad"), "Ctrl-s", _start).State;
        var id = saving.Session.Operations[0].Id;
        var error = new WriteError(
            WriteErrorKind.Validation,
            400,
            "bad",
            [new WriteFieldError(FormFieldIds.Labels, "Label is invalid")],
            []);

        var failed = _reducer.Reduce(saving, new WriteFailed(id, _start, error));

        var tab = failed.State.Session.Tabs[0];
        Assert.Null(tab.Form!.OperationId);
        Assert.Equal("Label is invalid", tab.Form.Fields[0].Error);
        Assert.Null(tab.Form.TopError);
        Assert.Equal(["backend"], tab.Rows[0].Labels);
        Assert.Empty(failed.State.Session.Operations);
        Assert.Equal(InputMode.Insert, failed.State.Mode);
    }

    [Fact]
    public void GivenAFailureWithoutFields_WhenJiraRefuses_ThenTheTopLineKeepsTheMessageAndTheTextIsKept()
    {
        var saving = Press(Type(Loaded([]), "mine"), "Ctrl-s", _start).State;
        var id = saving.Session.Operations[0].Id;
        var error = new WriteError(WriteErrorKind.Permission, 403, "no", [], []);

        var failed = _reducer.Reduce(saving, new WriteFailed(id, _start, error));

        var form = failed.State.Session.Tabs[0].Form!;
        Assert.Contains("No permission to edit A-1", form.TopError);
        Assert.Equal(["mine"], form.Fields[0].Value.Chosen);
    }

    [Fact]
    public void GivenALostReply_WhenTheWriteIsStillBeingChecked_ThenTheFormStaysLocked()
    {
        var saving = Press(Type(Loaded([]), "x"), "Ctrl-s", _start).State;
        var id = saving.Session.Operations[0].Id;
        var error = new WriteError(WriteErrorKind.Timeout, null, "slow", [], []);

        var unknown = _reducer.Reduce(saving, new WriteOutcomeUnknown(id, _start, error));

        Assert.True(unknown.State.Session.Tabs[0].Form!.IsSaving);
    }

    [Fact]
    public void GivenAnIssueUnderAWrite_WhenTheFormIsSubmitted_ThenItStaysOpenWithTheBusyReason()
    {
        var state = Loaded([]);
        var assign = new WriteRequested(state.Session.Tabs[0].Id, new AssignRequest("A-1", null), _start);
        var busy = _reducer.Reduce(state, assign);

        var submitted = Press(Type(busy.State, "x"), "Ctrl-s", _start);

        var form = submitted.State.Session.Tabs[0].Form!;
        Assert.False(form.IsSaving);
        Assert.Equal("A-1 is busy (assigning)", form.TopError);
        Assert.Single(submitted.State.Session.Operations);
    }

    [Fact]
    public void GivenAnUnchangedForm_WhenSubmitted_ThenNothingIsSentAndEscClosesWithoutAsking()
    {
        var state = Loaded(["backend"]);

        var submitted = Press(state, "Ctrl-s", _start);
        var closed = Press(submitted.State, "Esc");

        Assert.Empty(submitted.Effects);
        Assert.Equal("Nothing changed", submitted.State.Session.Tabs[0].Form!.TopError);
        Assert.Null(closed.State.Session.Tabs[0].Form);
    }

    [Fact]
    public void GivenAChangedForm_WhenEscIsPressed_ThenItAsksAndOnlyYDiscards()
    {
        var typed = Type(Loaded([]), "x");

        var asked = Press(typed, "Esc").State;
        var kept = Press(asked, "n").State;
        var askedAgain = Press(kept, "Esc").State;
        var discarded = Press(askedAgain, "y");

        Assert.True(asked.Session.Tabs[0].Form!.DiscardPending);
        Assert.False(kept.Session.Tabs[0].Form!.DiscardPending);
        Assert.Equal("x", kept.Session.Tabs[0].Form!.Fields[0].Entry);
        Assert.Null(discarded.State.Session.Tabs[0].Form);
        Assert.Equal(InputMode.Normal, discarded.State.Mode);
    }

    [Fact]
    public void GivenAFormWithRequiredAndOptionalFields_WhenTabbing_ThenFocusSkipsReadOnlyFieldsAndWraps()
    {
        var fields = new[]
        {
            FormField.Create("a", "A", FormFieldKind.Text, false, []),
            FormField.Create("b", "B", FormFieldKind.Unsupported, false, []),
            FormField.Create("c", "C", FormFieldKind.Number, false, []),
        };
        var state = LoadedWith(fields);

        var first = Press(state, "Tab").State.Session.Tabs[0].Form!.Focus;
        var wrapped = Press(Press(state, "Tab").State, "Tab").State.Session.Tabs[0].Form!.Focus;
        var back = Press(state, "Shift-Tab").State.Session.Tabs[0].Form!.Focus;

        Assert.Equal(2, first);
        Assert.Equal(0, wrapped);
        Assert.Equal(2, back);
    }

    [Fact]
    public void GivenARequiredFieldLeftEmpty_WhenSubmitted_ThenNoRequestIsSentAndTheFieldShowsTheError()
    {
        var fields = new[]
        {
            FormField.Create("note", "Note", FormFieldKind.Text, false, []),
            FormField.Create("summary", "Summary", FormFieldKind.Text, true, []),
        };
        var state = Type(LoadedWith(fields), "hello");

        var submitted = Press(state, "Ctrl-s", _start);

        var form = submitted.State.Session.Tabs[0].Form!;
        Assert.Empty(submitted.Effects);
        Assert.Equal(FormValidation.RequiredText, form.Fields[1].Error);
        Assert.Equal(1, form.Focus);
        Assert.Null(form.Fields[0].Error);
    }

    [Theory]
    [InlineData(FormFieldKind.Number, "12x", "Not a number")]
    [InlineData(FormFieldKind.Date, "2026-13-01", "Use yyyy-MM-dd")]
    [InlineData(FormFieldKind.DateTime, "2026-11-30", "Use yyyy-MM-dd HH:mm")]
    public void GivenATextThatDoesNotRead_WhenSubmitted_ThenTheFieldShowsWhatIsExpected(
        FormFieldKind kind,
        string text,
        string expected)
    {
        var state = Type(LoadedWith([FormField.Create("f", "F", kind, false, [])]), text);

        var submitted = Press(state, "Ctrl-s", _start);

        Assert.Empty(submitted.Effects);
        Assert.Equal(expected, submitted.State.Session.Tabs[0].Form!.Fields[0].Error);
    }

    [Fact]
    public void GivenAnUnsupportedRequiredField_WhenSubmitted_ThenItDoesNotBlockTheSave()
    {
        var fields = new[]
        {
            FormField.Create("a", "A", FormFieldKind.Text, false, []),
            FormField.Create("locked", "Locked", FormFieldKind.Unsupported, true, []),
        };

        var submitted = Press(Type(LoadedWith(fields), "x"), "Ctrl-s", _start);

        Assert.Single(submitted.Effects.OfType<ExecuteWriteEffect>());
    }

    [Fact]
    public void GivenAnEmptyLabelBufferWithManyLabels_WhenBackspacing_ThenTheLastLabelIsRemoved()
    {
        var state = Loaded(["a", "b"]);

        var removed = Press(state, "Backspace").State;

        Assert.Equal(["a"], removed.Session.Tabs[0].Form!.Fields[0].Value.Chosen);
    }

    [Fact]
    public void GivenAPaste_WhenAFormIsOpen_ThenTheFirstLineGoesToTheFocusedField()
    {
        var state = Loaded([]);

        var pasted = _reducer.Reduce(state, new TextPasted("one two\nthree")).State;

        var field = pasted.Session.Tabs[0].Form!.Fields[0];
        Assert.Equal(["one"], field.Value.Chosen);
        Assert.Equal("two", field.Entry);
    }

    [Fact]
    public void GivenTwoTabs_WhenTheFormOfOneIsOpen_ThenTheOtherTabHasNoForm()
    {
        var state = Loaded([]);
        var other = _reducer.Reduce(state, new ViewOpenRequested(
            new ViewDefinition(ViewSourceKind.Jql, "project = B", ViewLayout.List, "B"))).State;

        Assert.NotNull(other.Session.Tabs[0].Form);
        Assert.Null(other.Session.Tabs[1].Form);
    }

    private AppState Type(AppState state, string text)
    {
        return text.Aggregate(state, (current, character) => Press(current, character.ToString()).State);
    }

    private ReduceResult Press(AppState state, string key, DateTimeOffset at = default)
    {
        var spelled = key == " " ? "Space" : key;

        return _reducer.Reduce(state, new KeyPressed(spelled, at));
    }

    private ReduceResult Palette(AppState state, string command)
    {
        var current = state with { Mode = InputMode.Command, PaletteText = command };

        return _reducer.Reduce(current, new KeyPressed("Enter"));
    }

    private AppState Open()
    {
        return Palette(Tabbed(["backend"]), "labels").State;
    }

    private AppState Loaded(string[] labels)
    {
        var opened = Palette(Tabbed(labels), "labels").State;
        var tab = opened.Session.Tabs[0];

        var loaded = new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [LabelsField(labels)]);

        return _reducer.Reduce(opened, loaded).State;
    }

    private AppState LoadedWith(IReadOnlyList<FormField> fields)
    {
        var opened = Palette(Tabbed([]), "labels").State;
        var tab = opened.Session.Tabs[0];

        return _reducer.Reduce(opened, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, fields)).State;
    }

    private static FormField LabelsField(params string[] labels)
    {
        var field = FormField.Create(FormFieldIds.Labels, "Labels", FormFieldKind.Labels, false, []);

        return field.WithInitial(FormValue.OfChosen(labels));
    }

    private AppState Tabbed(string[] labels)
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
        var page = new IssuePage([Issue(labels)], null, null);

        return _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _start)).State;
    }

    private static IssueSummary Issue(string[] labels)
    {
        return new IssueSummary(
            "1",
            "A-1",
            "Issue 1",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            labels,
            null,
            null,
            "A");
    }
}
