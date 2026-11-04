using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Tests.Forms;

/// <summary>The navigate and edit states of the edit form (#650), through the real reducer and its keys.</summary>
[Trait("Os", "Independent")]
public class FormNavigationTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly FieldOption[] _options = [new("1", "Highest"), new("2", "High"), new("3", "Medium")];

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenALoadedEditForm_WhenItOpens_ThenItNavigatesOnTheFirstField()
    {
        var form = Form(Loaded());

        Assert.True(form.Navigable);
        Assert.True(form.Navigating);
        Assert.Equal(0, form.Focus);
    }

    [Theory]
    [InlineData("j")]
    [InlineData("Tab")]
    [InlineData("Down")]
    public void GivenNavigating_WhenMovingForward_ThenTheFocusMovesOneFieldAndNothingIsTyped(string key)
    {
        var moved = Press(Loaded(), key).State;

        var form = Form(moved);
        Assert.Equal(1, form.Focus);
        Assert.True(form.Navigating);
        Assert.Equal("Fix it", form.Fields[1].Value.Text);
    }

    [Theory]
    [InlineData("k")]
    [InlineData("Shift-Tab")]
    [InlineData("Up")]
    public void GivenNavigating_WhenMovingBack_ThenTheFocusWrapsToTheLastField(string key)
    {
        var form = Form(Press(Loaded(), key).State);

        Assert.Equal(1, form.Focus);
    }

    [Fact]
    public void GivenNavigating_WhenAPrintableKeyIsPressed_ThenTheFieldIsNotChanged()
    {
        var moved = Press(Loaded(), "x").State;

        Assert.False(Form(moved).IsDirty);
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData("i")]
    public void GivenNavigating_WhenEnterOrIIsPressed_ThenTheFocusedFieldIsEdited(string key)
    {
        var editing = Press(Press(Loaded(), "j").State, key).State;
        var typed = Press(editing, "x").State;

        Assert.False(Form(editing).Navigating);
        Assert.Equal("Fix itx", Form(typed).Fields[1].Value.Text);
    }

    [Fact]
    public void GivenEditing_WhenEscIsPressed_ThenItReturnsToNavigatingAndKeepsTheText()
    {
        var typed = Press(Press(Loaded(), "Enter").State, "x").State;

        var form = Form(Press(typed, "Esc").State);

        Assert.True(form.Navigating);
        Assert.False(form.DiscardPending);
        Assert.Equal("Fix itx", form.Fields[0].Value.Text);
    }

    [Fact]
    public void GivenEditingASingleLineField_WhenEnterIsPressed_ThenItReturnsToNavigatingOnTheSameField()
    {
        var form = Form(Press(Press(Loaded(), "Enter").State, "Enter").State);

        Assert.True(form.Navigating);
        Assert.Equal(0, form.Focus);
    }

    [Fact]
    public void GivenNavigatingWithChanges_WhenEscIsPressed_ThenTheFormAsksBeforeDiscarding()
    {
        var typed = Press(Press(Loaded(), "Enter").State, "x").State;
        var navigating = Press(typed, "Esc").State;

        var asked = Form(Press(navigating, "Esc").State);

        Assert.True(asked.DiscardPending);
    }

    [Fact]
    public void GivenNavigating_WhenAFieldIsClicked_ThenItIsFocusedAndEdited()
    {
        var clicked = new OverlayRowClicked(MouseTarget.FormField, 1, ActionIds.FormFieldNext, _start);

        var form = Form(_reducer.Reduce(Loaded(), clicked).State);

        Assert.Equal(1, form.Focus);
        Assert.False(form.Navigating);
    }

    [Fact]
    public void GivenNavigating_WhenTheFocusedFieldIsClicked_ThenItIsEdited()
    {
        var clicked = new OverlayRowClicked(MouseTarget.FormField, 0, ActionIds.FormFieldNext, _start);

        Assert.False(Form(_reducer.Reduce(Loaded(), clicked).State).Navigating);
    }

    [Fact]
    public void GivenNavigatingAChoiceField_WhenMoving_ThenNoPopupOpensUntilEditingStarts()
    {
        var navigating = Loaded(FormFieldKind.Select);

        Assert.Null(Form(navigating).Suggest);
        Assert.NotNull(Form(Press(navigating, "Enter").State).Suggest);
    }

    [Fact]
    public void GivenNavigating_WhenSaveIsPressed_ThenTheChangedFieldsAreSent()
    {
        var typed = Press(Press(Loaded(), "Enter").State, "x").State;
        var navigating = Press(typed, "Esc").State;

        var saved = Press(navigating, "Ctrl-s");

        Assert.Contains(saved.Effects, effect => effect is not CancelSuggestionsEffect);
    }

    [Fact]
    public void GivenAnEditFormWithOneEditableField_WhenItOpens_ThenItEditsAtOnce()
    {
        var opened = Palette(Tabbed(), "labels").State;
        var tab = opened.Session.Tabs[0];
        var summary = FormField.Create("summary", "Summary", FormFieldKind.Text, false, []);

        var loaded = _reducer.Reduce(opened, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [summary])).State;

        Assert.False(Form(loaded).Navigable);
        Assert.False(Form(loaded).Navigating);
    }

    private static FormState Form(AppState state)
    {
        return state.Session.Tabs[0].Form!;
    }

    private AppState Loaded(FormFieldKind first = FormFieldKind.Text)
    {
        var opened = Palette(Tabbed(), "labels").State;
        var tab = opened.Session.Tabs[0];
        var one = first == FormFieldKind.Select
            ? FormField.Create("priority", "Priority", first, false, _options)
            : FormField.Create("summary", "Summary", FormFieldKind.Text, false, []).WithInitial(FormValue.OfText("Fix it"));
        var two = FormField.Create("labelsText", "Other", FormFieldKind.Text, false, []).WithInitial(FormValue.OfText("Fix it"));

        return _reducer.Reduce(opened, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [one, two])).State;
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private ReduceResult Palette(AppState state, string command)
    {
        var current = state with { Mode = InputMode.Command, PaletteText = command };

        return _reducer.Reduce(current, new KeyPressed("Enter"));
    }

    private AppState Tabbed()
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
        var page = new IssuePage([Issue()], null, null);

        return _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _start)).State;
    }

    private static IssueSummary Issue()
    {
        return new IssueSummary(
            "1",
            "A-1",
            "Issue 1",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            ["backend"],
            null,
            null,
            "A");
    }
}
