using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Tests.Forms;

/// <summary>The suggestion popup of select and multi-select fields, through the real reducer and its keys.</summary>
[Trait("Os", "Independent")]
public class FormChoiceTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly FieldOption[] _options = [new("1", "Highest"), new("2", "High"), new("3", "Medium")];

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenARequiredSingleChoiceWithoutAValue_WhenTheFormLoads_ThenAllValuesShowOnTheFirst()
    {
        var state = Focused(FormFieldKind.Select, true);

        var popup = Popup(state);
        Assert.Equal(["Highest", "High", "Medium"], popup.Items.Select(item => item.Label));
        Assert.Equal(0, popup.Cursor);
        Assert.Equal(SuggestionKind.Options, popup.Source.Kind);
        Assert.Equal(0, popup.RequestId);
    }

    [Fact]
    public void GivenAnOptionalSingleChoice_WhenFocused_ThenNoneIsOfferedFirst()
    {
        var state = Focused(FormFieldKind.Select, false);

        Assert.Equal(["(none)", "Highest", "High", "Medium"], Popup(state).Items.Select(item => item.Label));
        Assert.Equal(-1, Popup(state).Cursor);
    }

    [Fact]
    public void GivenAFilterIsTyped_WhenRanking_ThenNoJiraCallIsRequestedAndTheBestMatchIsHighlighted()
    {
        var typed = Press(Focused(FormFieldKind.Select, true), "m");

        Assert.Empty(typed.Effects);
        Assert.Equal(["Medium"], Popup(typed.State).Items.Select(item => item.Label));
        Assert.Equal(0, Popup(typed.State).Cursor);
    }

    [Fact]
    public void GivenAMatch_WhenEnterIsPressed_ThenTheSingleChoiceTakesItAndTheFormReturnsToNavigating()
    {
        var typed = Press(Focused(FormFieldKind.Select, true), "m");

        var accepted = Press(typed.State, "Enter");

        var form = accepted.State.Session.Tabs[0].Form!;
        Assert.Equal(["3"], form.Fields[0].Value.Chosen);
        Assert.Equal(string.Empty, form.Fields[0].Entry);
        Assert.Equal(0, form.Focus);
        Assert.True(form.Navigating);
        Assert.Null(form.Suggest);
    }

    [Fact]
    public void GivenARequiredEmptyChoice_WhenTabIsPressed_ThenTheFocusMovesOnWithoutPickingTheFirstValue()
    {
        var moved = Press(Focused(FormFieldKind.Select, true), "Tab");

        var form = moved.State.Session.Tabs[0].Form!;
        Assert.Empty(form.Fields[0].Value.Chosen);
        Assert.Equal(1, form.Focus);
    }

    [Fact]
    public void GivenAChoiceWithAValue_WhenTheFormOpens_ThenNothingIsHighlightedAndEnterKeepsTheValue()
    {
        var state = Focused(FormFieldKind.Select, true, "2");

        var entered = Press(state, "Enter");

        Assert.Equal(-1, Popup(state).Cursor);
        Assert.Equal(["2"], entered.State.Session.Tabs[0].Form!.Fields[0].Value.Chosen);
        Assert.True(entered.State.Session.Tabs[0].Form!.Navigating);
    }

    [Fact]
    public void GivenTheDownKey_WhenNothingIsHighlighted_ThenTheFirstValueIsHighlightedAndAccepted()
    {
        var moved = Press(Focused(FormFieldKind.Select, false), "Down");
        moved = Press(moved.State, "Down");

        var accepted = Press(moved.State, "Enter");

        Assert.Equal(["1"], accepted.State.Session.Tabs[0].Form!.Fields[0].Value.Chosen);
    }

    [Fact]
    public void GivenAMultiChoice_WhenValuesAreAccepted_ThenTheyBecomeChipsAndLeaveTheList()
    {
        var first = Press(Press(Focused(FormFieldKind.MultiSelect, false), "Down").State, "Enter");
        var second = Press(Press(first.State, "Down").State, "Enter");

        var form = second.State.Session.Tabs[0].Form!;
        Assert.Equal(["1", "2"], form.Fields[0].Value.Chosen);
        Assert.Equal(["Medium"], form.Suggest!.Items.Select(item => item.Label));
        Assert.Equal(0, form.Focus);
    }

    [Fact]
    public void GivenAFilterTypedInAChoice_WhenEscIsPressed_ThenOnlyTheFilterIsDropped()
    {
        var typed = Press(Focused(FormFieldKind.Select, true), "m");

        var dismissed = Press(typed.State, "Esc");

        var form = dismissed.State.Session.Tabs[0].Form!;
        Assert.Equal(string.Empty, form.Fields[0].Entry);
        Assert.Equal(3, form.Suggest!.Items.Count);
        Assert.False(form.DiscardPending);
    }

    [Fact]
    public void GivenNoMatch_WhenEnterIsPressed_ThenTheFormMovesOnWithoutAValue()
    {
        var typed = Press(Focused(FormFieldKind.Select, true), "z");

        var entered = Press(typed.State, "Enter");

        Assert.Empty(Popup(typed.State).Items);
        Assert.Empty(entered.State.Session.Tabs[0].Form!.Fields[0].Value.Chosen);
    }

    [Fact]
    public void GivenTheFocusMovesToATextField_WhenTheNextFieldKeyIsPressed_ThenTheChoicePopupCloses()
    {
        var moved = Press(Focused(FormFieldKind.Select, true), "Shift-Tab");

        var form = moved.State.Session.Tabs[0].Form!;
        Assert.Equal(1, form.Focus);
        Assert.Null(form.Suggest);
    }

    [Fact]
    public void GivenTheFocusLeftTheChoice_WhenItReturns_ThenItsPopupReopens()
    {
        var there = Press(Focused(FormFieldKind.Select, true), "Shift-Tab");

        var back = Press(there.State, "Tab");

        Assert.Equal(SuggestionKind.Options, back.State.Session.Tabs[0].Form!.Suggest!.Source.Kind);
    }

    private static SuggestionState Popup(AppState state)
    {
        return state.Session.Tabs[0].Form!.Suggest!;
    }

    // The form with the choice field first (focused) and a summary text field second.
    private AppState Focused(FormFieldKind kind, bool required, string? chosen = null)
    {
        var opened = Palette(Tabbed(), "labels").State;
        var tab = opened.Session.Tabs[0];
        var choice = FormField.Create("priority", "Priority", kind, required, _options);
        choice = chosen is null ? choice : choice.WithInitial(FormValue.OfChosen([chosen]));
        var summary = FormField.Create("summary", "Summary", FormFieldKind.Text, false, []);

        var loaded = _reducer.Reduce(opened, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [choice, summary]));

        // The edit form opens in the navigate state; Enter starts editing the first field (#650).
        return Press(loaded.State, "Enter").State;
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
