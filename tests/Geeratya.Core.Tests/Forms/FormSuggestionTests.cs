using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Tests.Forms;

/// <summary>The suggestion popup inside the field form, through the real reducer and its keys.</summary>
[Trait("Os", "Independent")]
public class FormSuggestionTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenTheLabelsField_WhenTheFirstCharacterIsTyped_ThenALookupIsRequestedWithTheDebounce()
    {
        var result = Press(Loaded(), "b");

        var effect = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(result.Effects));
        var popup = Popup(result.State);
        Assert.Equal(TimeSpan.FromMilliseconds(250), effect.Debounce);
        Assert.Equal("b", effect.Query);
        Assert.Equal(SuggestionKind.Labels, effect.Source.Kind);
        Assert.Contains("backend", effect.Source.KnownLabels);
        Assert.Equal(effect.RequestId, popup.RequestId);
        Assert.Contains(popup.Items, item => item.Label == "backend");
    }

    [Fact]
    public void GivenAnEmptyBuffer_WhenNothingIsTyped_ThenNoPopupOpens()
    {
        var state = Loaded();

        Assert.Null(state.Session.Tabs[0].Form!.Suggest);
    }

    [Fact]
    public void GivenTwoKeystrokes_WhenTheFirstReplyArrivesLate_ThenOnlyTheNewestRequestIsApplied()
    {
        var first = Press(Loaded(), "b");
        var second = Press(first.State, "a");
        var firstId = Assert.IsType<SearchSuggestionsEffect>(first.Effects[0]).RequestId;
        var secondId = Assert.IsType<SearchSuggestionsEffect>(second.Effects[0]).RequestId;

        var late = Reply(second.State, firstId, Label("stale-label"));
        var current = Reply(late.State, secondId, Label("balance"));

        Assert.NotEqual(firstId, secondId);
        Assert.DoesNotContain(Popup(late.State).Items, item => item.Label == "stale-label");
        Assert.Equal(SuggestionStatus.Waiting, Popup(late.State).Status);
        Assert.Contains(Popup(current.State).Items, item => item.Label == "balance");
        Assert.Equal(SuggestionStatus.Loaded, Popup(current.State).Status);
    }

    [Fact]
    public void GivenAReplyForAnOldRequest_WhenThePopupWasDismissed_ThenNothingReopens()
    {
        var typed = Press(Loaded(), "b");
        var id = Assert.IsType<SearchSuggestionsEffect>(typed.Effects[0]).RequestId;
        var dismissed = Press(typed.State, "Esc").State;

        var late = Reply(dismissed, id, Label("backend"));

        Assert.Null(late.State.Session.Tabs[0].Form!.Suggest);
    }

    [Fact]
    public void GivenAnOpenPopup_WhenEscIsPressed_ThenOnlyThePopupClosesAndTheLookupIsCancelled()
    {
        var typed = Press(Loaded(), "b");

        var dismissed = Press(typed.State, "Esc");

        var form = dismissed.State.Session.Tabs[0].Form!;
        Assert.Null(form.Suggest);
        Assert.False(form.DiscardPending);
        Assert.Equal(InputMode.Insert, dismissed.State.Mode);
        Assert.IsType<CancelSuggestionsEffect>(Assert.Single(dismissed.Effects));
        Assert.Equal("b", form.Fields[0].Entry);
    }

    [Fact]
    public void GivenAHighlightedLabel_WhenTabIsPressed_ThenItIsAddedAndThePopupCloses()
    {
        var typed = Press(Loaded(), "b");

        var accepted = Press(typed.State, "Tab");

        var form = accepted.State.Session.Tabs[0].Form!;
        Assert.Equal(["backend"], form.Fields[0].Value.Chosen);
        Assert.Equal(string.Empty, form.Fields[0].Entry);
        Assert.Null(form.Suggest);
        Assert.Contains(accepted.Effects, effect => effect is CancelSuggestionsEffect);
    }

    [Fact]
    public void GivenTwoAcceptedLabels_WhenTheFieldIsSubmitted_ThenBothAreWrittenAsAdds()
    {
        var state = Press(Press(Press(Loaded(), "b").State, "Enter").State, "f").State;
        state = Press(state, "Enter").State;

        var submitted = Press(state, "Ctrl-s", _start);

        var request = Assert.IsType<EditRequest>(Assert.Single(submitted.Effects.OfType<ExecuteWriteEffect>()).Request);
        Assert.Equal(["backend", "frontend"], Assert.Single(request.Changes).Value.Chosen);
    }

    [Fact]
    public void GivenATextThatMatchesNothing_WhenEnterIsPressed_ThenItIsAddedAsANewLabel()
    {
        var typed = Press(Press(Press(Loaded(), "z").State, "z").State, "z");
        var id = Assert.IsType<SearchSuggestionsEffect>(typed.Effects[0]).RequestId;
        var replied = Reply(typed.State, id);

        Assert.Equal("zzz", Popup(replied.State).Selected!.Id);

        var accepted = Press(replied.State, "Enter");

        Assert.Equal(["zzz"], accepted.State.Session.Tabs[0].Form!.Fields[0].Value.Chosen);
    }

    [Fact]
    public void GivenATypedLabel_WhenSpaceIsPressed_ThenItIsCommittedAndThePopupClosesWithoutALookup()
    {
        var typed = Press(Press(Loaded(), "a").State, "b");

        var spaced = Press(typed.State, "Space");

        var form = spaced.State.Session.Tabs[0].Form!;
        Assert.Equal(["ab"], form.Fields[0].Value.Chosen);
        Assert.Null(form.Suggest);
        Assert.DoesNotContain(spaced.Effects, effect => effect is SearchSuggestionsEffect);
    }

    [Fact]
    public void GivenThreeMatches_WhenDownAndUpAreUsed_ThenTheCursorMovesAndStaysInsideTheList()
    {
        var typed = Press(Loaded(), "e");
        var id = Assert.IsType<SearchSuggestionsEffect>(typed.Effects[0]).RequestId;
        var state = Reply(typed.State, id, Label("alpha-e"), Label("beta-e")).State;

        var down = Press(Press(state, "Down").State, "Ctrl-n").State;
        var up = Press(Press(down, "Up").State, "Ctrl-p").State;
        var top = Press(up, "Up").State;

        Assert.Equal(2, Popup(down).Cursor);
        Assert.Equal(0, Popup(up).Cursor);
        Assert.Equal(0, Popup(top).Cursor);
    }

    [Fact]
    public void GivenAnOpenPopup_WhenTheLookupIsSlowThenFails_ThenTheListStaysAndTheErrorShows()
    {
        var typed = Press(Loaded(), "b");
        var id = Assert.IsType<SearchSuggestionsEffect>(typed.Effects[0]).RequestId;
        var tab = typed.State.Session.Tabs[0].Id;

        var slow = _reducer.Reduce(typed.State, new SuggestionsSlow(tab, id));
        var failed = _reducer.Reduce(slow.State, new SuggestionsFailed(tab, id, "offline"));

        Assert.Equal(SuggestionStatus.Loading, Popup(slow.State).Status);
        Assert.Equal(SuggestionStatus.Failed, Popup(failed.State).Status);
        Assert.Equal("offline", Popup(failed.State).Error);
        Assert.Contains(Popup(failed.State).Items, item => item.Label == "backend");
    }

    [Fact]
    public void GivenAReplyWhileAnotherFieldHasTheFocus_WhenItArrives_ThenItIsIgnored()
    {
        var typed = Press(Loaded(), "b");
        var id = Assert.IsType<SearchSuggestionsEffect>(typed.Effects[0]).RequestId;
        var moved = Press(typed.State, "Shift-Tab");

        var late = Reply(moved.State, id, Label("backend"));

        Assert.Null(late.State.Session.Tabs[0].Form!.Suggest);
        Assert.Contains(moved.Effects, effect => effect is CancelSuggestionsEffect);
    }

    [Fact]
    public void GivenAPaste_WhenTheLabelsFieldHasTheFocus_ThenThePopupOpensForTheFirstWord()
    {
        var pasted = _reducer.Reduce(Loaded(), new TextPasted("back"));

        var effect = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(pasted.Effects));
        Assert.Equal("back", effect.Query);
    }

    [Fact]
    public void GivenAUserField_WhenAPersonIsAccepted_ThenTheAccountIdIsTheValueAndTheNameShows()
    {
        var state = Focused("assignee");
        var typed = Press(Press(state, "j").State, "o");
        var id = Assert.IsType<SearchSuggestionsEffect>(typed.Effects[0]).RequestId;
        var replied = Reply(typed.State, id, new Suggestion("acc-1", "Jo Smith", null, false));

        var accepted = Press(replied.State, "Enter");

        var field = accepted.State.Session.Tabs[0].Form!.Fields[1];
        Assert.Equal(["acc-1"], field.Value.Chosen);
        Assert.Equal("Jo Smith", field.Options.Single().Label);
        Assert.Equal(SuggestionKind.AssignableUsers, Assert.IsType<SearchSuggestionsEffect>(typed.Effects[0]).Source.Kind);
    }

    [Fact]
    public void GivenAnAcceptedPerson_WhenTheFormIsSaved_ThenTheWriteCarriesTheAccountId()
    {
        var typed = Press(Focused("assignee"), "j");
        var id = Assert.IsType<SearchSuggestionsEffect>(typed.Effects[0]).RequestId;
        var replied = Reply(typed.State, id, new Suggestion("acc-1", "Jo Smith", null, false));
        var accepted = Press(replied.State, "Tab").State;

        var saved = Press(accepted, "Ctrl-s", _start);

        var request = Assert.IsType<EditRequest>(Assert.Single(saved.Effects.OfType<ExecuteWriteEffect>()).Request);
        var change = Assert.Single(request.Changes);
        Assert.Equal(FormFieldKind.User, change.Kind);
        Assert.Equal(["acc-1"], change.Value.Chosen);
    }

    [Fact]
    public void GivenAChosenPerson_WhenBackspaceIsPressedOnAnEmptyBuffer_ThenTheValueIsCleared()
    {
        var typed = Press(Focused("assignee"), "j");
        var id = Assert.IsType<SearchSuggestionsEffect>(typed.Effects[0]).RequestId;
        var replied = Reply(typed.State, id, new Suggestion("acc-1", "Jo Smith", null, false));
        var accepted = Press(replied.State, "Enter").State;

        var cleared = Press(accepted, "Backspace").State;

        Assert.True(cleared.Session.Tabs[0].Form!.Fields[1].Value.IsEmpty);
    }

    [Fact]
    public void GivenAPersonNobodyPicked_WhenEnterIsPressedWithText_ThenTheFocusStaysAndNoValueIsSet()
    {
        var typed = Press(Focused("assignee"), "j");
        var empty = Reply(typed.State, ((SearchSuggestionsEffect)typed.Effects[0]).RequestId);

        var entered = Press(empty.State, "Enter").State;

        var form = entered.Session.Tabs[0].Form!;
        Assert.Equal(1, form.Focus);
        Assert.True(form.Fields[1].Value.IsEmpty);
    }

    [Fact]
    public void GivenTheFullLabelTyped_WhenEnterIsPressed_ThenThatLabelIsAddedNotALongerOne()
    {
        var typed = Press(Press(Press(Loaded(), "b").State, "u").State, "g");
        var id = Assert.IsType<SearchSuggestionsEffect>(typed.Effects[0]).RequestId;
        var replied = Reply(typed.State, id, Label("bugfix"), Label("bug"));

        var accepted = Press(replied.State, "Enter");

        Assert.Equal(["bug"], accepted.State.Session.Tabs[0].Form!.Fields[0].Value.Chosen);
    }

    [Fact]
    public void GivenTextTypedInAPersonFieldButNobodyPicked_WhenSaving_ThenTheFieldSaysToPickAndNothingIsSent()
    {
        var typed = Press(Focused("assignee"), "a");
        var left = Press(typed.State, "Tab").State;

        var saved = Press(left, "Ctrl-s", _start);

        var field = saved.State.Session.Tabs[0].Form!.Fields[1];
        Assert.Equal(FormValidation.PickText, field.Error);
        Assert.Empty(saved.Effects.OfType<ExecuteWriteEffect>());
    }

    [Fact]
    public void GivenOnlyTextTypedInAPersonField_WhenEscIsPressedTwice_ThenTheFormAsksBeforeDiscarding()
    {
        var typed = Press(Focused("assignee"), "a");
        var dismissed = Press(typed.State, "Esc").State;

        var asked = Press(dismissed, "Esc").State;

        Assert.True(asked.Session.Tabs[0].Form!.DiscardPending);
    }

    [Fact]
    public void GivenAParentField_WhenTyping_ThenIssuesAreSearched()
    {
        var typed = Press(Focused("parent", FormFieldKind.Parent), "A");

        var effect = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(typed.Effects));
        Assert.Equal(SuggestionKind.Issues, effect.Source.Kind);
        Assert.Equal("A-1", effect.Source.IssueKey);
    }

    [Fact]
    public void GivenAnotherUserField_WhenTyping_ThenPeopleWhoCanSeeTheIssueAreSearched()
    {
        var typed = Press(Focused("customfield_1"), "a");

        var effect = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(typed.Effects));
        Assert.Equal(SuggestionKind.IssueUsers, effect.Source.Kind);
    }

    [Fact]
    public void GivenAUserAndAParentField_WhenTheFormLoads_ThenBothAreEditableAndTakeTheFocus()
    {
        var state = Focused("assignee");

        var form = state.Session.Tabs[0].Form!;
        Assert.All(form.Fields, field => Assert.True(field.IsEditable));
    }

    private AppState Loaded()
    {
        return Focused(null);
    }

    // The form with labels first and, when given, a field of the given kind second and focused.
    private AppState Focused(string? fieldId, FormFieldKind kind = FormFieldKind.User)
    {
        var opened = Palette(Tabbed(), "labels").State;
        var tab = opened.Session.Tabs[0];
        var labels = FormField.Create(FormFieldIds.Labels, "Labels", FormFieldKind.Labels, false, [])
            .WithInitial(FormValue.OfChosen([]));
        var fields = new List<FormField> { labels };

        if (fieldId is not null)
        {
            fields.Add(FormField.Create(fieldId, "Person", kind, false, []));
        }

        var loaded = _reducer.Reduce(opened, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, fields)).State;

        if (fieldId is null)
        {
            return loaded;
        }

        var form = loaded.Session.Tabs[0].Form!;
        var focused = form with { Focus = 1 };

        return WithTab(loaded, loaded.Session.Tabs[0] with { Form = focused });
    }

    private static AppState WithTab(AppState state, TabState tab)
    {
        return state with { Session = state.Session with { Tabs = [tab] } };
    }

    private static SuggestionState Popup(AppState state)
    {
        return state.Session.Tabs[0].Form!.Suggest!;
    }

    private static Suggestion Label(string text)
    {
        return new Suggestion(text, text, null, false);
    }

    private ReduceResult Reply(AppState state, long requestId, params Suggestion[] items)
    {
        var tab = state.Session.Tabs[0].Id;

        return _reducer.Reduce(state, new SuggestionsLoaded(tab, requestId, items));
    }

    private ReduceResult Press(AppState state, string key, DateTimeOffset at = default)
    {
        return _reducer.Reduce(state, new KeyPressed(key, at));
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
            ["backend", "frontend"],
            null,
            null,
            "A");
    }
}
