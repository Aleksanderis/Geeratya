using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Forms;

/// <summary>
/// One contract for every person field (Assignee, Reporter, a custom user picker) however its form is opened: the
/// create form , the assign form, the boxed edit
/// form and the transition fields. The quick rows and the search source follow the field and the context, never the
/// kind of form (<c>docs/internal/ui-consistency.md</c>, "Shared components", 2).
/// </summary>
[Trait("Os", "Independent")]
public class PersonFieldContractTests
{
    private const string Me = "acc-me";
    private const string Eve = "acc-eve";

    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    /// <summary>Every way a person field is shown: the form target and whether it searches by issue or by project.</summary>
    public static TheoryData<string> Presentations =>
    [
        "create",
        "assign",
        "edit",
        "transition",
    ];

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAnEmptyOptionalPersonField_WhenItGetsTheFocus_ThenOnlyMeIsOfferedAndNothingIsHighlighted(
        string presentation)
    {
        var popup = Popup(Open(presentation, FormFieldIds.Assignee, null, false));

        var row = Assert.Single(popup.Items);
        Assert.Equal(Me, row.Id);
        Assert.Equal(AssigneeQuickRows.MeDetail, row.Detail);
        Assert.Null(popup.Selected);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAnOptionalPersonFieldHoldingSomeoneElse_WhenItGetsTheFocus_ThenMeAndUnassignedAreOffered(
        string presentation)
    {
        var popup = Popup(Open(presentation, FormFieldIds.Assignee, Eve, false));

        Assert.Equal([Me, string.Empty], popup.Items.Select(item => item.Id));
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAnOptionalPersonFieldHoldingMe_WhenItGetsTheFocus_ThenOnlyUnassignedIsOffered(string presentation)
    {
        var popup = Popup(Open(presentation, FormFieldIds.Assignee, Me, false));

        var row = Assert.Single(popup.Items);
        Assert.Equal(string.Empty, row.Id);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenARequiredPersonFieldHoldingSomeoneElse_WhenItGetsTheFocus_ThenUnassignedIsNotOffered(
        string presentation)
    {
        var popup = Popup(Open(presentation, FormFieldIds.Reporter, Eve, true));

        var row = Assert.Single(popup.Items);
        Assert.Equal(Me, row.Id);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenARequiredPersonFieldHoldingMe_WhenItGetsTheFocus_ThenNoQuickRowIsOffered(string presentation)
    {
        var state = Open(presentation, FormFieldIds.Reporter, Me, true);

        Assert.Null(state.Session.Tabs[0].Form!.Suggest);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAnAssigneeField_WhenTyping_ThenAssignablePeopleAreSearchedInTheContextOfTheForm(
        string presentation)
    {
        var effect = Typed(presentation, FormFieldIds.Assignee);

        Assert.Equal(SuggestionKind.AssignableUsers, effect.Source.Kind);
        AssertContext(presentation, effect);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAnotherPersonField_WhenTyping_ThenPeopleWhoSeeTheIssueAreSearchedInTheContextOfTheForm(
        string presentation)
    {
        var effect = Typed(presentation, FormFieldIds.Reporter);

        var create = presentation.StartsWith("create", StringComparison.Ordinal);
        Assert.Equal(create ? SuggestionKind.ProjectUsers : SuggestionKind.IssueUsers, effect.Source.Kind);
        AssertContext(presentation, effect);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenTheMeRow_WhenItIsTaken_ThenTheFieldHoldsTheAccountAndOnlyUnassignedRemainsOffered(string presentation)
    {
        var opened = Open(presentation, FormFieldIds.Assignee, null, false);

        var taken = Press(Press(opened, "Down").State, "Tab").State.Session.Tabs[0].Form!;

        var field = taken.Fields[0];
        Assert.Equal([Me], field.Value.Chosen);
        Assert.Equal(string.Empty, Assert.Single(taken.Suggest!.Items).Id);
    }

    [Fact]
    public void GivenTheCreateForm_WhenTheMetadataListsTheReporter_ThenItIsPrefilledWithTheSignedInAccountAndNotSent()
    {
        var state = Open("create", FormFieldIds.Reporter, null, true);

        var field = state.Session.Tabs[0].Form!.Fields[0];
        Assert.Equal([Me], field.Value.Chosen);
        Assert.False(field.IsChanged);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenCreateMetadata_WhenMapped_ThenTheReporterIsOfferedOnlyWhenTheMetadataListsIt(bool listed)
    {
        var assignee = new CreateField(FormFieldIds.Assignee, "Assignee", false, false, CreateFieldKind.User, []);
        var reporter = new CreateField(FormFieldIds.Reporter, "Reporter", true, true, CreateFieldKind.User, []);
        CreateField[] metadata = listed ? [assignee, reporter] : [assignee];

        var fields = FormFieldMapper.FromCreate(metadata);

        Assert.Equal(listed, fields.Any(field => field.Id == FormFieldIds.Reporter));
    }

    private static void AssertContext(string presentation, SearchSuggestionsEffect effect)
    {
        if (presentation.StartsWith("create", StringComparison.Ordinal))
        {
            Assert.Equal("A", effect.Source.ProjectKey);
            Assert.Null(effect.Source.IssueKey);

            return;
        }

        Assert.Equal("A-1", effect.Source.IssueKey);
        Assert.Null(effect.Source.ProjectKey);
    }

    private static FormTarget TargetOf(string presentation)
    {
        return presentation switch
        {
            "create" => new CreateFormTarget("A", null, false, null),
            "assign" => new AssignFormTarget("A-1", null),
            "edit" => new EditFormTarget("A-1", new Dictionary<string, FormValue>(), false, null),
            _ => new TransitionFormTarget("A-1", Choice()),
        };
    }

    private static TransitionChoice Choice()
    {
        return new TransitionChoice("41", "Done", new IssueStatus("3", "Done", "done"), true, true, []);
    }

    private static SuggestionState Popup(AppState state)
    {
        return state.Session.Tabs[0].Form!.Suggest!;
    }

    // The form with the one person field, focused.
    private AppState Open(string presentation, string fieldId, string? holder, bool required)
    {
        var state = Tabbed();
        var tab = state.Session.Tabs[0];
        var form = FormState.Opened(TargetOf(presentation), 7, null);
        var withForm = state with
        {
            Session = state.Session with { Tabs = [tab with { Form = form }] },
            Mode = InputMode.Insert,
        };
        var field = FormField.Create(fieldId, "Person", FormFieldKind.User, required, []);
        var shown = holder is null ? field : field.WithInitial(FormValue.OfChosen([holder]));
        var loaded = new FormMetadataLoaded(tab.Id, 7, [shown]);

        return _reducer.Reduce(withForm, loaded).State;
    }

    private SearchSuggestionsEffect Typed(string presentation, string fieldId)
    {
        var opened = Open(presentation, fieldId, null, false);
        var result = Press(opened, "j");

        return Assert.IsType<SearchSuggestionsEffect>(Assert.Single(result.Effects));
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key, _start));
    }

    private AppState Tabbed()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me", Me) },
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
            [],
            null,
            null,
            "A");
    }
}
