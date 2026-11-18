using System.Globalization;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Links;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Core.Writes;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Tests.Forms;

/// <summary>
/// The behaviour contract of #1022: one key script per field kind, driven through every form presentation, with the
/// same outcome in each (<c>docs/internal/ui-consistency.md</c>, "Behaviour contract"). The table is
/// <see cref="Kinds"/> (every <see cref="FormFieldKind"/>) by <see cref="PresentationNames"/> (every way a form is
/// shown); a guard fails when a kind, a presentation or a form target is missing from it. The person fields are the
/// contract of <see cref="PersonFieldContractTests"/>.
/// </summary>
[Trait("Os", "Independent")]
public class FieldBehaviourContractTests
{
    private const string Me = "acc-me";
    private const string CompanionId = "customfield_note";
    private const string PickId = "customfield_pick";

    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    /// <summary>What the quick rows read when a field opens with no popup.</summary>
    public const string NoPopup = "no popup";

    /// <summary>
    /// The Due date block of the issue view, opened by Enter through the real route (the cursor stop of the
    /// Details pane, <c>DetailFieldActions</c>, <c>FormOpener</c>): a date field in a dropdown, which must behave as
    /// the same kind does on the create form and in the edit box (#1036).
    /// </summary>
    public const string DetailsDue = "details-due";

    /// <summary>The ways a form is shown. Each opens the given fields; the first is the field under test.</summary>
    public static IReadOnlyList<string> PresentationNames { get; } =
    [
        "create",
        "create-subtask",
        "edit",
        "dropdown",
        "details-due",
        "in-place",
        "transition",
        "transition-pick",
        "link",
        "sprint",
        "sprint-start",
        "sprint-complete",
        "sprint-move",
    ];

    /// <summary>
    /// The form targets of Core and the presentations that open them. A new <c>FormTarget</c> that is missing here
    /// fails <see cref="GivenTheFormTargetsOfCore_WhenTheTableIsChecked_ThenEachOneIsInAPresentation"/>.
    /// </summary>
    public static IReadOnlyDictionary<Type, string[]> TargetPresentations { get; } = new Dictionary<Type, string[]>
    {
        [typeof(CreateFormTarget)] = ["create", "create-subtask"],
        [typeof(EditFormTarget)] = ["edit", "dropdown", "details-due", "in-place"],
        [typeof(TransitionFormTarget)] = ["transition"],
        [typeof(TransitionPickTarget)] = ["transition-pick"],
        [typeof(LinkFormTarget)] = ["link"],
        [typeof(SprintFormTarget)] = ["sprint"],
        [typeof(StartSprintFormTarget)] = ["sprint-start"],
        [typeof(CompleteSprintFormTarget)] = ["sprint-complete"],
        [typeof(SprintMoveTarget)] = ["sprint-move"],

        // The assign form holds the one person field; its contract is PersonFieldContractTests.
        [typeof(AssignFormTarget)] = [],
    };

    /// <summary>
    /// Every field kind with the key script that fills it with a valid value, a text that is wrong for it (kinds
    /// with a format) and whether this contract drives it.
    /// </summary>
    public static IReadOnlyList<KindRow> Kinds { get; } =
    [
        new(FormFieldKind.Text, "hello", null, "text=hello;chosen=", true),
        new(FormFieldKind.Paragraph, "hello", null, "text=hello;chosen=", true),
        new(FormFieldKind.Number, "3", "1x", "text=3;chosen=", true),
        new(FormFieldKind.Date, "2026-12-01", "2026-1", "text=2026-12-01;chosen=", true),
        new(
            FormFieldKind.DateTime,
            "2026-12-01|Space|10:30",
            "2026-12-01|Space|1",
            "text=2026-12-01 10:30;chosen=",
            true),
        new(FormFieldKind.Select, "Down|Down|Enter", null, "text=;chosen=low", true, ",low,base", "click:1"),
        new(FormFieldKind.MultiSelect, "Down|Enter", null, "text=;chosen=low", true, "low,base", "click:0"),
        new(FormFieldKind.Labels, "x|Enter", null, "text=;chosen=x", true, NoPopup, "x|click:0"),
        new(
            FormFieldKind.Parent,
            "a-9|Enter",
            null,
            "text=;chosen=A-9",
            true,
            NoPopup,
            "a-|reply:A-9=A-9 Epic|click:0"),
        new(FormFieldKind.User, string.Empty, null, string.Empty, false),
        new(FormFieldKind.Unsupported, string.Empty, null, string.Empty, false),
    ];

    /// <summary>
    /// The forms whose write reads fields by ids of its own (the status picker, the sprint move, the destination of
    /// a completed sprint, the dates of a sprint start): a field of the table has no meaning to their request, so
    /// the save scenarios skip them (they still run the open, typing and Esc scenarios).
    /// </summary>
    public static IReadOnlySet<string> OwnFields { get; } = new HashSet<string>
    {
        "transition-pick",
        "sprint-move",
        "sprint-complete",
        "sprint-start",
    };

    /// <summary>The forms of one choice: only a choice field is offered there.</summary>
    public static IReadOnlySet<string> OneChoice { get; } = new HashSet<string>
    {
        "transition-pick",
        "sprint-move",
        "sprint-complete",
    };

    /// <summary>
    /// The keys that take the second row of each one-choice form. The status picker is required and has no value,
    /// so it starts on its first row; the sprint move and the completed sprint start with a value and nothing
    /// highlighted (<c>ChoicePopup</c>), so they need one key more.
    /// </summary>
    public static IReadOnlyDictionary<string, string> OwnChoiceKeys { get; } = new Dictionary<string, string>
    {
        ["transition-pick"] = "Down|Enter",
        ["sprint-move"] = "Down|Down|Enter",
        ["sprint-complete"] = "Down|Down|Enter",
    };

    /// <summary>
    /// The presentations with no issue and no project to search in: a parent field has no source there, so none is
    /// offered (a sprint form holds names and dates only).
    /// </summary>
    public static IReadOnlySet<string> NoSearch { get; } = new HashSet<string> { "sprint", "sprint-start" };

    /// <summary>
    /// The presentations that open with their fields known and fetch nothing (<c>FormReducer.OpenReady</c>); the
    /// others load their metadata first. They must end the same: quick rows, popup, focus.
    /// </summary>
    public static IReadOnlySet<string> Prepared { get; } = new HashSet<string>
    {
        "sprint",
        "sprint-start",
        "sprint-complete",
        "sprint-move",
    };

    /// <summary>
    /// The presentations whose write carries the changed fields (an edit, a create or a transition), so the payload
    /// of the field under test can be read from the queued request.
    /// </summary>
    public static IReadOnlySet<string> ChangeCarriers { get; } = new HashSet<string>
    {
        "create",
        "create-subtask",
        "edit",
        "dropdown",
        "details-due",
        "in-place",
        "transition",
    };

    /// <summary>
    /// The presentations whose request reads named fields of its own (the status picker, the link with its type and
    /// issue, the sprint forms with their name and dates), so a field of the table is not in its payload.
    /// </summary>
    public static IReadOnlySet<string> NoChanges { get; } = new HashSet<string>
    {
        "transition-pick",
        "link",
        "sprint",
        "sprint-start",
        "sprint-complete",
        "sprint-move",
    };

    /// <summary>What the discard question names in each presentation that asks one (a form with a typed change).</summary>
    public static IReadOnlyDictionary<string, string> DiscardNames { get; } = new Dictionary<string, string>
    {
        ["create"] = "the new issue",
        ["create-subtask"] = "the new issue",
        ["edit"] = "the changes to A-1",
        ["dropdown"] = "the changes to A-1",
        ["details-due"] = "the changes to A-1",
        ["in-place"] = "the changes to A-1",
        ["transition"] = "the changes to A-1",
        ["link"] = "the changes to A-1",
        ["sprint"] = "the new sprint",
        ["sprint-start"] = "the changes to Sprint 5",
    };

    public static TheoryData<FormFieldKind, string> Cells
    {
        get
        {
            var data = new TheoryData<FormFieldKind, string>();

            foreach (var row in Kinds.Where(static row => row.Editable))
            {
                foreach (var presentation in row.Presentations())
                {
                    data.Add(row.Kind, presentation);
                }
            }

            return data;
        }
    }

    /// <summary>One row of the table: a field kind, its valid and wrong key scripts and the value it ends in.</summary>
    /// <param name="Kind">The field kind.</param>
    /// <param name="Valid">The keys that give the field a valid value, split by <c>|</c>.</param>
    /// <param name="Wrong">A script that types a text the format refuses, or null for a kind with no format.</param>
    /// <param name="Expected">The value the field holds after the valid script (text and chosen values).</param>
    /// <param name="Editable">False for a kind this contract does not drive (a person field, a read-only kind).</param>
    /// <param name="Quick">
    /// The ids of the rows the popup offers on open (the quick rows), joined by <c>,</c>, or <see cref="NoPopup"/>:
    /// typed text has no rows, and labels and a parent have none until something is typed (no label applied yet,
    /// no search yet).
    /// </param>
    /// <param name="Click">The script that takes the value by a click on a row instead of the key.</param>
    public sealed record KindRow(
        FormFieldKind Kind,
        string Valid,
        string? Wrong,
        string Expected,
        bool Editable,
        string Quick = NoPopup,
        string? Click = null)
    {
        /// <summary>The presentations that offer the kind: the in-place edit holds only the text of a pane.</summary>
        public IEnumerable<string> Presentations()
        {
            var text = Kind is FormFieldKind.Text or FormFieldKind.Paragraph;
            var choice = Kind == FormFieldKind.Select;
            var date = Kind == FormFieldKind.Date;

            return PresentationNames
                .Where(name => text || name != "in-place")
                .Where(name => date || name != DetailsDue)
                .Where(name => Kind != FormFieldKind.Parent || !NoSearch.Contains(name))
                .Where(name => choice || !OneChoice.Contains(name));
        }

        /// <summary>The presentations whose write reads the field of the table, not fields of their own.</summary>
        public IEnumerable<string> Writing()
        {
            return Presentations().Where(name => !OwnFields.Contains(name));
        }
    }

    /// <summary>What a script leaves behind: whether a write started, the error of the field and its value.</summary>
    public sealed record Outcome(bool Sent, string Error, string Value);

    /// <summary>
    /// Where a script ends: the phase of the form (<c>editing</c>, <c>navigating</c>, <c>closed</c>), the value the
    /// form holds or the write carries, and whether a write started.
    /// </summary>
    public sealed record Ending(string Phase, string Value, bool Sent);

    [Fact]
    public void GivenTheFieldKinds_WhenTheTableIsChecked_ThenEveryKindIsInIt()
    {
        var missing = Enum.GetValues<FormFieldKind>().Except(Kinds.Select(static row => row.Kind)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void GivenTheFormTargetsOfCore_WhenTheTableIsChecked_ThenEachOneIsInAPresentation()
    {
        // Reflection is fine here: the guard runs in the test project, which is never trimmed.
#pragma warning disable IL2026
        var targets = typeof(FormTarget).Assembly.GetTypes()
            .Where(static type => type.IsSubclassOf(typeof(FormTarget)) && !type.IsAbstract)
            .ToList();
#pragma warning restore IL2026

        var missing = targets.Where(type => !TargetPresentations.ContainsKey(type)).Select(type => type.Name).ToList();
        var named = TargetPresentations.Values.SelectMany(static names => names).ToList();

        Assert.Empty(missing);
        Assert.Equal(PresentationNames.Order(), named.Order());
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void GivenAFieldKind_WhenItIsEdited_ThenTheCellOpensAndTheFieldHasTheFocus(
        FormFieldKind kind,
        string presentation)
    {
        var state = Edit(Open(presentation, Field(kind)));

        var form = state.Session.Tabs[0].Form!;
        Assert.Equal(kind, form.Fields[form.Focus].Kind);
        Assert.Equal(InputMode.Insert, state.Mode);
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void GivenAFieldKind_WhenJAndKAreTyped_ThenTheyAreTextOfTheFieldOnEveryPresentation(
        FormFieldKind kind,
        string presentation)
    {
        var state = Type(Edit(Open(presentation, Field(kind))), "jk");

        var field = FocusField(state);
        var typed = FormFieldKinds.IsText(kind) ? field.Value.Text : field.Entry;
        Assert.Equal("jk", typed);
        Assert.Equal(InputMode.Insert, state.Mode);
    }

    [Fact]
    public void GivenAFieldKind_WhenTheKeyScriptRuns_ThenEveryPresentationEndsWithTheExpectedValueAndAWrite()
    {
        var differences = new List<string>();

        foreach (var row in Kinds.Where(static row => row.Editable))
        {
            var seen = row.Writing().ToDictionary(name => name, name => Valid(row, name));
            var expected = new Outcome(true, string.Empty, row.Expected);

            differences.AddRange(seen
                .Where(pair => pair.Value != expected)
                .Select(pair => $"{row.Kind}: {pair.Key} = {pair.Value}, expected {expected}"));
        }

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    [Fact]
    public void GivenAWrongText_WhenTheFormIsSaved_ThenEveryPresentationRefusesWithTheSameErrorAndSendsNothing()
    {
        var differences = new List<string>();

        foreach (var row in Kinds.Where(static row => row.Wrong is not null))
        {
            var seen = row.Writing().ToDictionary(name => name, name => Wrong(row, name));
            var first = seen.First();

            Assert.False(first.Value.Sent, $"{row.Kind} sent a wrong text on {first.Key}");
            Assert.False(string.IsNullOrEmpty(first.Value.Error), $"{row.Kind} has no error on {first.Key}");
            differences.AddRange(seen
                .Where(pair => pair.Value != first.Value)
                .Select(pair => $"{row.Kind}: {pair.Key} = {pair.Value}, {first.Key} = {first.Value}"));
        }

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    [Fact]
    public void GivenATypedFilterOrText_WhenEscIsPressed_ThenTheLadderIsTheSameAndOnlyNavigableFormsHaveTheExtraStep()
    {
        var differences = new List<string>();

        foreach (var row in Kinds.Where(static row => row.Editable))
        {
            var ladders = row.Presentations()
                .ToDictionary(name => name, name => Ladder(Type(Edit(Open(name, Field(row.Kind))), Filter(row.Kind))));
            var first = ladders.First();

            differences.AddRange(ladders
                .Where(pair => Plain(pair.Value) != Plain(first.Value))
                .Select(pair => $"{row.Kind}: {pair.Key} = {Plain(pair.Value)}, {first.Key} = {Plain(first.Value)}"));
        }

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void GivenTheNavigateState_WhenQIsPressed_ThenItDoesWhatEscDoes(FormFieldKind kind, string presentation)
    {
        var state = Type(Edit(Open(presentation, Field(kind))), "jk");
        var navigating = Press(state, "Esc").State;

        if (navigating.Session.Tabs[0].Form is not { Navigating: true })
        {
            return;
        }

        var byEsc = LadderLabel(Press(navigating, "Esc").State);
        var byQ = LadderLabel(Press(navigating, "q").State);

        Assert.Equal(byEsc, byQ);
    }

    [Fact]
    public void GivenAFieldKind_WhenItOpens_ThenEveryPresentationOffersTheSameQuickRows()
    {
        var differences = new List<string>();

        foreach (var row in Kinds.Where(static row => row.Editable))
        {
            foreach (var presentation in row.Presentations())
            {
                var seen = QuickRows(Edit(Open(presentation, Field(row.Kind))));

                if (seen != row.Quick)
                {
                    differences.Add($"{row.Kind}: {presentation} offers '{seen}', expected '{row.Quick}'");
                }
            }
        }

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    [Fact]
    public void GivenARowOfTheList_WhenItIsClicked_ThenEveryPresentationDoesWhatTheKeyDoes()
    {
        var differences = new List<string>();

        foreach (var row in Kinds.Where(static row => row.Click is not null))
        {
            foreach (var presentation in row.Writing())
            {
                var byKey = Trace(row, presentation, row.Valid);
                var byClick = Trace(row, presentation, row.Click!);

                if (byKey != byClick)
                {
                    differences.Add($"{row.Kind}: {presentation} click = {byClick}, key = {byKey}");
                }
            }
        }

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    [Fact]
    public void GivenAClickedRow_WhenTheFormEnds_ThenEveryPresentationHoldsOrWritesTheExpectedValue()
    {
        var differences = new List<string>();

        foreach (var row in Kinds.Where(static row => row.Click is not null))
        {
            foreach (var presentation in row.Writing())
            {
                var trace = Trace(row, presentation, row.Click!);

                if (!trace.Value.EndsWith($"|{row.Kind}|{row.Expected}", StringComparison.Ordinal))
                {
                    differences.Add($"{row.Kind}: {presentation} clicked to {trace.Value}, expected {row.Expected}");
                }
            }
        }

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    [Fact]
    public void GivenAValidScript_WhenTheFormIsSaved_ThenEveryPresentationQueuesTheSamePayloadForTheField()
    {
        var differences = new List<string>();

        foreach (var row in Kinds.Where(static row => row.Editable))
        {
            var field = Field(row.Kind);
            var expected = $"{field.Id}|{row.Kind}|{row.Expected}";

            foreach (var presentation in row.Writing().Where(static name => ChangeCarriers.Contains(name)))
            {
                var state = Edit(Open(presentation, field));

                state = Script(state, row.Valid);
                state = Press(state, "Ctrl-s").State;

                var seen = Payload(state, row.Kind);

                if (seen != expected)
                {
                    differences.Add($"{row.Kind}: {presentation} queued '{seen}', expected '{expected}'");
                }
            }
        }

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    [Fact]
    public void GivenTheChangeCarriers_WhenTheTableIsChecked_ThenEachIsAPresentationAndTheOthersAreListedAsIntended()
    {
        Assert.All(ChangeCarriers, name => Assert.Contains(name, PresentationNames));
        Assert.All(NoChanges, name => Assert.Contains(name, PresentationNames));
        Assert.Equal(PresentationNames.Order(), ChangeCarriers.Concat(NoChanges).Order());
    }

    [Fact]
    public void GivenATypedText_WhenEscLeavesTheForm_ThenTheDiscardQuestionNamesWhatWouldBeLost()
    {
        var differences = new List<string>();
        var text = Kinds.Single(static row => row.Kind == FormFieldKind.Text);

        foreach (var presentation in text.Presentations())
        {
            var state = Type(Edit(Open(presentation, Field(text.Kind))), "jk");

            for (var step = 0; step < 3 && state.Confirm is null; step++)
            {
                state = Press(state, "Esc").State;
            }

            var asked = state.Confirm?.Message ?? "no question";
            var expected = $"Discard {DiscardNames[presentation]}? Nothing has been saved.";

            if (asked != expected)
            {
                differences.Add($"{presentation}: asks '{asked}', expected '{expected}'");
            }
        }

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    [Fact]
    public void GivenTheCompletedSprintForm_WhenEscIsPressedUntouched_ThenItAsksWhatWouldBeLost()
    {
        var state = Edit(Open("sprint-complete", OwnChoice("sprint-complete")));

        for (var step = 0; step < 3 && state.Confirm is null; step++)
        {
            state = Press(state, "Esc").State;
        }

        // The destination is a value the form proposes, so it counts as typed (docs: intended differences).
        Assert.Equal("Discard the changes to Sprint 5? Nothing has been saved.", state.Confirm?.Message);
    }

    [Fact]
    public void GivenAOneChoiceForm_WhenItsSecondRowIsClicked_ThenItDoesWhatTheKeysDo()
    {
        var differences = new List<string>();

        foreach (var presentation in OneChoice)
        {
            var byKey = OwnEnding(presentation, OwnChoiceKeys[presentation]);
            var byClick = OwnEnding(presentation, "click:1");

            if (byKey != byClick)
            {
                differences.Add($"{presentation}: click = {byClick}, key = {byKey}");
            }

            // The status picker and the sprint move write at once; the completed sprint asks Complete on save.
            Assert.Equal(presentation != "sprint-complete", byClick.Sent);
        }

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    // Unsupported is the only read-only kind here; person fields are PersonFieldContractTests.
    [Fact]
    public void GivenAReadOnlyKind_WhenKeysAreTyped_ThenNothingChangesOnAnyPresentation()
    {
        const FormFieldKind kind = FormFieldKind.Unsupported;

        foreach (var presentation in PresentationNames.Where(static name => name != "in-place"))
        {
            var opened = Open(presentation, Field(kind));

            var typed = Type(opened, "jk");

            var field = typed.Session.Tabs[0].Form!.Fields.First(candidate => candidate.Kind == kind);

            Assert.Equal(string.Empty, field.Value.Text);
            Assert.Equal(string.Empty, field.Entry);
        }
    }

    // The ids of the popup rows of the focused field, or "no popup".
    private static string QuickRows(AppState state)
    {
        if (state.Session.Tabs[0].Form?.Suggest is not { } popup)
        {
            return NoPopup;
        }

        return string.Join(',', popup.Items.Select(static item => item.Id));
    }

    // The field after a script (a click or keys), without a Ctrl-s: its value is the form's or, when the script
    // wrote at once (a one-field form), the one of the queued request.
    private Ending Trace(KindRow row, string presentation, string script)
    {
        var state = Script(Edit(Open(presentation, Field(row.Kind))), script);
        var sent = state.Session.Operations.Count > 0;
        var form = state.Session.Tabs[0].Form;

        if (sent)
        {
            return new Ending("closed", Payload(state, row.Kind), true);
        }

        var field = form!.Fields.First(candidate => candidate.Kind == row.Kind);
        var phase = form.Navigating ? "navigating" : "editing";

        return new Ending(phase, $"{field.Id}|{row.Kind}|{Describe(field.Value)}", false);
    }

    // The change of the given kind in the last queued request, as id, kind and value.
    private static string Payload(AppState state, FormFieldKind kind)
    {
        if (state.Session.Operations.Count == 0)
        {
            return "nothing queued";
        }

        var changes = state.Session.Operations[^1].Request switch
        {
            EditRequest edit => edit.Changes,
            CreateRequest create => create.Changes,
            TransitionRequest transition => transition.Changes,
            _ => null,
        };

        if (changes is null)
        {
            return "no changes in the request";
        }

        var change = changes.FirstOrDefault(candidate => candidate.Kind == kind);

        return change is null ? "field missing" : $"{change.FieldId}|{kind}|{Describe(change.Value)}";
    }

    // Esc until the form is gone or the discard question is asked; one label per step.
    private List<string> Ladder(AppState state)
    {
        var steps = new List<string>();

        for (var step = 0; step < 5; step++)
        {
            state = Press(state, "Esc").State;
            var form = state.Session.Tabs[0].Form;
            var label = LadderLabel(state, form);
            steps.Add(label);

            if (label is "closed" or "ask")
            {
                break;
            }
        }

        return steps;
    }

    private static string LadderLabel(AppState state)
    {
        return LadderLabel(state, state.Session.Tabs[0].Form);
    }

    private static string LadderLabel(AppState state, FormState? form)
    {
        if (state.Confirm is not null)
        {
            return "ask";
        }

        if (form is null)
        {
            return "closed";
        }

        return form.Navigating ? "navigate" : "stay";
    }

    private static string Plain(List<string> ladder)
    {
        return string.Join('>', ladder.Where(static step => step != "navigate"));
    }

    private static string Filter(FormFieldKind kind)
    {
        return FormFieldKinds.IsChoice(kind) ? "ba" : "jk";
    }

    private Outcome Valid(KindRow row, string presentation)
    {
        var state = Edit(Open(presentation, Field(row.Kind)));
        var before = state.Session.Operations.Count;

        state = Script(state, row.Valid);
        state = Press(state, "Ctrl-s").State;

        return OutcomeOf(state, before);
    }

    private Outcome Wrong(KindRow row, string presentation)
    {
        var state = Edit(Open(presentation, Field(row.Kind)));
        var before = state.Session.Operations.Count;

        state = Script(state, row.Wrong!);
        state = Press(state, "Ctrl-s").State;

        return OutcomeOf(state, before);
    }

    private static Outcome OutcomeOf(AppState state, int operationsBefore)
    {
        var sent = state.Session.Operations.Count > operationsBefore || state.Confirm is not null;
        var form = state.Session.Tabs[0].Form;
        var field = form?.Fields.FirstOrDefault(candidate => candidate.Id != CompanionId);

        return new Outcome(
            sent,
            field?.Error ?? string.Empty,
            field is null ? string.Empty : Describe(field.Value));
    }

    private static string Describe(FormValue value)
    {
        return $"text={value.Text};chosen={string.Join(',', value.Chosen)}";
    }

    // A script is text typed, or keys joined by '|' (a part of one character is a key name only when longer).
    private AppState Script(AppState state, string script)
    {
        foreach (var part in script.Split('|'))
        {
            state = Step(state, part);
        }

        return state;
    }

    // A click on row n of the open list (the mouse path), a key name or typed text.
    private AppState Step(AppState state, string part)
    {
        if (part.StartsWith("click:", StringComparison.Ordinal))
        {
            var index = int.Parse(part["click:".Length..], CultureInfo.InvariantCulture);
            var clicked = new OverlayRowClicked(MouseTarget.FormChoice, index, string.Empty, _start);

            return _reducer.Reduce(state, clicked).State;
        }

        if (part.StartsWith("reply:", StringComparison.Ordinal))
        {
            return Reply(state, part["reply:".Length..]);
        }

        return part.Length > 1 && char.IsUpper(part[0]) ? Press(state, part).State : Type(state, part);
    }

    // The open search answers with one row, written id=Label.
    private AppState Reply(AppState state, string row)
    {
        var parts = row.Split('=');
        var tab = state.Session.Tabs[0];
        var item = new Suggestion(parts[0], parts[1], null, false);

        return _reducer.Reduce(state, new SuggestionsLoaded(tab.Id, tab.Form!.Suggest!.RequestId, [item])).State;
    }

    private AppState Type(AppState state, string text)
    {
        return text.Aggregate(state, (current, character) => Press(current, character.ToString()).State);
    }

    // A navigable form opens in its navigate state or editing; Enter on the field starts the editing.
    private AppState Edit(AppState state)
    {
        return state.Session.Tabs[0].Form is { Navigating: true } ? Press(state, "Enter").State : state;
    }

    private static FormField FocusField(AppState state)
    {
        var form = state.Session.Tabs[0].Form!;

        return form.Fields[form.Focus];
    }

    private static FormField Field(FormFieldKind kind)
    {
        FieldOption[] options = [new("low", "Low"), new("base", "Base")];

        return kind switch
        {
            FormFieldKind.Labels => FormField.Create(FormFieldIds.Labels, "Labels", kind, false, []),
            FormFieldKind.Parent => FormField.Create(FormFieldIds.Parent, "Parent", kind, false, []),
            FormFieldKind.User => FormField.Create("customfield_person", "Person", kind, false, []),
            FormFieldKind.Select or FormFieldKind.MultiSelect => FormField.Create(PickId, "Pick", kind, false, options),
            _ => FormField.Create($"customfield_{kind}", kind.ToString(), kind, false, []),
        };
    }

    private static FormField Companion()
    {
        return FormField.Create(CompanionId, "Note", FormFieldKind.Text, false, []);
    }

    // The one choice field that a one-choice form builds from its own data.
    private static FormField OwnChoice(string presentation)
    {
        return TargetOf(presentation) switch
        {
            TransitionPickTarget pick => pick.Fields()[0],
            SprintMoveTarget move => move.Field(),
            CompleteSprintFormTarget complete => complete.Fields()[0],
            _ => throw new ArgumentOutOfRangeException(nameof(presentation), presentation, null),
        };
    }

    private Ending OwnEnding(string presentation, string script)
    {
        var state = Script(Edit(Open(presentation, OwnChoice(presentation))), script);

        if (state.Session.Operations.Count > 0)
        {
            return new Ending("closed", WrittenChoice(state.Session.Operations[^1].Request), true);
        }

        var form = state.Session.Tabs[0].Form;

        if (form is null)
        {
            return new Ending("closed", $"confirm={state.Confirm is not null}", false);
        }

        return new Ending(form.Navigating ? "navigating" : "editing", Describe(form.Fields[0].Value), false);
    }

    private static string WrittenChoice(WriteRequest request)
    {
        return request switch
        {
            TransitionRequest transition => $"transition {transition.Choice.Id}",
            SprintMoveRequest move => $"sprint {move.Target?.Id}",
            _ => request.GetType().Name,
        };
    }

    private AppState Open(string presentation, FormField field)
    {
        var single = presentation is "dropdown" or "in-place" or "transition-pick" or "sprint-move"
            or "sprint-complete";
        FormField[] fields = single ? [field] : [field, Companion()];

        if (Prepared.Contains(presentation))
        {
            var tabbed = Tabbed();

            return FormReducer.OpenReady(tabbed, tabbed.Session.Tabs[0].Id, TargetOf(presentation), fields).State;
        }

        if (presentation == DetailsDue)
        {
            return OpenDetailsDue(field);
        }

        var form = FormState.Opened(TargetOf(presentation), 7, null);

        if (presentation == "dropdown")
        {
            form = form with { Dropdown = field.Id };
        }

        if (presentation == "in-place")
        {
            form = form with { Pane = field.Kind == FormFieldKind.Paragraph ? PaneIds.Description : PaneIds.Title };
        }

        var state = Tabbed();
        var tab = state.Session.Tabs[0];
        var withForm = state with
        {
            Session = state.Session with { Tabs = [tab with { Form = form }] },
            Mode = InputMode.Insert,
        };

        return _reducer.Reduce(withForm, new FormMetadataLoaded(tab.Id, 7, fields)).State;
    }

    private AppState OpenDetailsDue(FormField field)
    {
        var listed = Tabbed();
        var tab = listed.Session.Tabs[0];
        var shown = Press(listed, "Enter").State;
        var request = shown.Session.Tabs[0].Detail[0].IssueRequest;
        var issue = new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            null,
            [],
            null,
            null,
            "A",
            null,
            null,
            [],
            [],
            []);
        var loaded = _reducer.Reduce(shown, new IssueDetailLoaded(tab.Id, request, issue, _start)).State;
        var editable = _reducer.Reduce(loaded, new IssueEditableLoaded(tab.Id, "A-1", [FormFieldIds.DueDate])).State;
        var onField = MoveTo(editable, DetailFieldIds.DueDate);
        var opened = Press(onField, "Enter").State;
        var form = opened.Session.Tabs[0].Form!;

        Assert.Equal(DetailFieldIds.DueDate, form.Dropdown);

        return _reducer.Reduce(opened, new FormMetadataLoaded(tab.Id, form.RequestId, [field])).State;
    }

    private AppState MoveTo(AppState state, string fieldId)
    {
        for (var step = 0; step < 40; step++)
        {
            var top = state.Session.Tabs[0].Detail[^1];

            if (top.Document!.Targets[top.Cursor] is { Kind: DetailTargetKind.Field } target && target.Value == fieldId)
            {
                return state;
            }

            state = Press(state, "j").State;
        }

        throw new InvalidOperationException($"No field {fieldId} in the issue view");
    }

    private static FormTarget TargetOf(string presentation)
    {
        var sprint = new Sprint(5, "Sprint 5", SprintState.Future, null, null, null, null, 1);
        var tally = new SprintTally(3, 5, ["A-1"], false);
        var type = new CreateIssueType("10", "Task", string.Empty, 0, false);
        var subtask = new CreateIssueType("11", "Sub-task", string.Empty, -1, true);

        return presentation switch
        {
            "create" => new CreateFormTarget("A", null, false, type),
            "create-subtask" => new CreateFormTarget("A", "A-1", true, subtask),
            "transition" => new TransitionFormTarget("A-1", Transition()),
            "transition-pick" => new TransitionPickTarget("A-1", [], null, PickChoices()),
            "link" => new LinkFormTarget("A-1"),
            "sprint" => new SprintFormTarget(1, null),
            "sprint-start" => new StartSprintFormTarget(1, sprint, tally),
            "sprint-complete" => new CompleteSprintFormTarget(1, sprint, tally, sprint),
            "sprint-move" => new SprintMoveTarget("A-1", [sprint], null, "board"),
            _ => new EditFormTarget("A-1", new Dictionary<string, FormValue>(), false, null),
        };
    }

    private static TransitionChoice[] PickChoices()
    {
        var todo = new IssueStatus("1", "To Do", "new");
        var started = new IssueStatus("2", "In Progress", "indeterminate");

        return
        [
            new TransitionChoice("11", "To Do", todo, false, false, []),
            new TransitionChoice("21", "In Progress", started, false, false, []),
        ];
    }

    private static TransitionChoice Transition()
    {
        return new TransitionChoice("41", "Done", new IssueStatus("3", "Done", "done"), true, true, []);
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
