using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Forms;

/// <summary>
/// One contract for each field kind however its form is opened: the create form, the boxed edit form, the dropdown of
/// the issue view and the transition fields. Quick rows, search source, typed keys and format checks follow the kind
/// and the context, never the kind of form (<c>docs/internal/ui-consistency.md</c>, "Shared components", 2).
/// </summary>
[Trait("Os", "Independent")]
public class FieldKindContractTests
{
    private const string Me = "acc-me";

    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    public static TheoryData<string> Presentations =>
    [
        "create",
        "edit",
        "dropdown",
        "transition",
    ];

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenALabelsFieldWithLabels_WhenItGetsTheFocus_ThenTheAppliedLabelsAreListedForRemoval(
        string presentation)
    {
        var state = Open(presentation, Labels("backend", "frontend"));

        var popup = state.Session.Tabs[0].Form!.Suggest!;
        Assert.Equal(["backend", "frontend"], popup.Items.Select(item => item.Id));
        Assert.All(popup.Items, item => Assert.Equal(LabelQuickRows.AppliedDetail, item.Detail));
        Assert.Null(popup.Selected);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAnAppliedLabel_WhenItsRowIsTaken_ThenTheLabelIsRemoved(string presentation)
    {
        var state = Open(presentation, Labels("backend", "frontend"));

        var taken = Press(Press(state, "Down").State, "Tab").State.Session.Tabs[0].Form!;

        Assert.Equal(["frontend"], taken.Fields[0].Value.Chosen);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenALabelsFieldWithNoLabels_WhenItGetsTheFocus_ThenNoRowIsListed(string presentation)
    {
        var state = Open(presentation, Labels());

        Assert.Null(state.Session.Tabs[0].Form!.Suggest);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAFullIssueKeyTypedIntoAParentField_WhenEnterIsPressed_ThenTheKeyIsTakenAsItStands(
        string presentation)
    {
        var state = Open(presentation, Parent(), Text());

        var typed = Type(state, "ab-12");
        var entered = Press(typed, "Enter").State.Session.Tabs[0].Form!;

        Assert.Equal(["AB-12"], entered.Fields[0].Value.Chosen);
        Assert.Equal(string.Empty, entered.Fields[0].Entry);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAParentField_WhenTyping_ThenIssuesAreSearchedInTheContextOfTheForm(string presentation)
    {
        var state = Open(presentation, Parent());

        var effect = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(Press(state, "j").Effects));

        Assert.Equal(SuggestionKind.Issues, effect.Source.Kind);

        if (presentation == "create")
        {
            Assert.Null(effect.Source.IssueKey);
            Assert.Equal("A", effect.Source.ProjectKey);

            return;
        }

        Assert.Equal("A-1", effect.Source.IssueKey);
        Assert.Equal(presentation == "transition" ? null : "A", effect.Source.ProjectKey);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenANumberField_WhenTyping_ThenTheFormatIsCheckedAsItIsTyped(string presentation)
    {
        var state = Open(presentation, Field("Points", FormFieldKind.Number));

        var wrong = Type(state, "1x").Session.Tabs[0].Form!.Fields[0];
        var fixedUp = Press(Type(state, "1x"), "Backspace").State.Session.Tabs[0].Form!.Fields[0];

        Assert.Equal(FormValidation.NumberText, wrong.Error);
        Assert.Null(fixedUp.Error);
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenADateField_WhenTyping_ThenTheFormatIsCheckedAsItIsTypedAndAnEmptyFieldHasNoError(
        string presentation)
    {
        var state = Open(presentation, Field("Due", FormFieldKind.Date));

        var partial = Type(state, "2026-1").Session.Tabs[0].Form!.Fields[0];
        var complete = Type(state, "2026-12-01").Session.Tabs[0].Form!.Fields[0];
        var cleared = Press(Type(state, "2"), "Backspace").State.Session.Tabs[0].Form!.Fields[0];

        Assert.Equal(FormValidation.DateText, partial.Error);
        Assert.Null(complete.Error);
        Assert.Null(cleared.Error);
    }

    [Theory]
    [InlineData(FormFieldKind.Date, "Due date (yyyy-MM-dd)")]
    [InlineData(FormFieldKind.DateTime, "Due date (yyyy-MM-dd HH:mm)")]
    [InlineData(FormFieldKind.Text, "Due date")]
    public void GivenADateKind_WhenTheFieldIsCreated_ThenItsLabelSpellsOutTheFormat(FormFieldKind kind, string expected)
    {
        Assert.Equal(expected, FormField.Create("duedate", "Due date", kind, false, []).Label);
    }

    [Fact]
    public void GivenALabelAlreadyNamingTheFormat_WhenTheFieldIsCreated_ThenTheFormatIsNotRepeated()
    {
        var field = FormField.Create("start", "Start (yyyy-MM-dd HH:mm)", FormFieldKind.DateTime, false, []);

        Assert.Equal("Start (yyyy-MM-dd HH:mm)", field.Label);
    }

    private static FormField Labels(params string[] labels)
    {
        var field = Field("Labels", FormFieldKind.Labels) with { Id = FormFieldIds.Labels };

        return labels.Length == 0 ? field : field.WithInitial(FormValue.OfChosen(labels));
    }

    private static FormField Parent()
    {
        return Field("Parent", FormFieldKind.Parent) with { Id = FormFieldIds.Parent };
    }

    private static FormField Text()
    {
        return Field("Note", FormFieldKind.Text);
    }

    private static FormField Field(string label, FormFieldKind kind)
    {
        return FormField.Create($"customfield_{label}", label, kind, false, []);
    }

    private AppState Type(AppState state, string text)
    {
        return text.Aggregate(state, (current, character) => Press(current, character.ToString()).State);
    }

    private static FormTarget TargetOf(string presentation)
    {
        return presentation switch
        {
            "create" => new CreateFormTarget("A", null, false, null),
            "edit" or "dropdown" => new EditFormTarget("A-1", new Dictionary<string, FormValue>(), false, null),
            _ => new TransitionFormTarget("A-1", Choice()),
        };
    }

    private static TransitionChoice Choice()
    {
        return new TransitionChoice("41", "Done", new IssueStatus("3", "Done", "done"), true, true, []);
    }

    // The form with the given fields, the first focused.
    private AppState Open(string presentation, params FormField[] fields)
    {
        var state = Tabbed();
        var tab = state.Session.Tabs[0];
        var form = FormState.Opened(TargetOf(presentation), 7, null) with
        {
            Dropdown = presentation == "dropdown" ? fields[0].Id : null,
        };
        var withForm = state with
        {
            Session = state.Session with { Tabs = [tab with { Form = form }] },
            Mode = InputMode.Insert,
        };
        var loaded = new FormMetadataLoaded(tab.Id, 7, fields);

        return _reducer.Reduce(withForm, loaded).State;
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
