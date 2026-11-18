using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Links;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
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

    /// <summary>The ways a form is shown. Each opens the given fields; the first is the field under test.</summary>
    public static IReadOnlyList<string> PresentationNames { get; } =
    [
        "create",
        "create-subtask",
        "edit",
        "dropdown",
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
        [typeof(EditFormTarget)] = ["edit", "dropdown", "in-place"],
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
        new(FormFieldKind.Text, "hello", null, true),
        new(FormFieldKind.Paragraph, "hello", null, true),
        new(FormFieldKind.Number, "3", "1x", true),
        new(FormFieldKind.Date, "2026-12-01", "2026-1", true),
        new(FormFieldKind.DateTime, "2026-12-01 10:30", "2026-12-01 1", true),
        new(FormFieldKind.Select, "Down|Down|Enter", null, true),
        new(FormFieldKind.MultiSelect, "Down|Enter", null, true),
        new(FormFieldKind.Labels, "x|Enter", null, true),
        new(FormFieldKind.Parent, "ab-12|Enter", null, true),
        new(FormFieldKind.User, string.Empty, null, false),
        new(FormFieldKind.Unsupported, string.Empty, null, false),
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
    /// The presentations with no issue and no project to search in: a parent field has no source there, so none is
    /// offered (a sprint form holds names and dates only).
    /// </summary>
    public static IReadOnlySet<string> NoSearch { get; } = new HashSet<string> { "sprint", "sprint-start" };

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
    public void GivenAFieldKind_WhenItIsEdited_ThenTheCellOpensAndTheFieldHasTheFocus(FormFieldKind kind, string presentation)
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
    public void GivenAFieldKind_WhenTheKeyScriptRuns_ThenEveryPresentationEndsWithTheSameValueErrorAndSend()
    {
        var differences = new List<string>();

        foreach (var row in Kinds.Where(static row => row.Editable))
        {
            var seen = row.Writing().ToDictionary(name => name, name => Valid(row, name));
            var first = seen.First();

            differences.AddRange(seen
                .Where(pair => pair.Value != first.Value)
                .Select(pair => $"{row.Kind}: {pair.Key} = {pair.Value}, {first.Key} = {first.Value}"));
        }

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
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

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
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

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
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

    [Theory]
    [InlineData(FormFieldKind.Unsupported)]
    public void GivenAReadOnlyKind_WhenKeysAreTyped_ThenNothingChangesOnAnyPresentation(FormFieldKind kind)
    {
        foreach (var presentation in PresentationNames.Where(static name => name != "in-place"))
        {
            var opened = Open(presentation, Field(kind));

            var typed = Type(opened, "jk");

            Assert.All(typed.Session.Tabs[0].Form!.Fields.Where(field => field.Kind == kind), field =>
            {
                Assert.Equal(string.Empty, field.Value.Text);
                Assert.Equal(string.Empty, field.Entry);
            });
        }
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

        try
        {
            state = Press(state, "Ctrl-s").State;
        }
        catch (InvalidOperationException exception)
        {
            return new Outcome(false, $"throws {exception.Message}", string.Empty);
        }

        return OutcomeOf(state, before);
    }

    private Outcome Wrong(KindRow row, string presentation)
    {
        var state = Edit(Open(presentation, Field(row.Kind)));
        var before = state.Session.Operations.Count;

        state = Type(state, row.Wrong!);
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
            state = part.Length > 1 && char.IsUpper(part[0]) ? Press(state, part).State : Type(state, part);
        }

        return state;
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

    private AppState Open(string presentation, FormField field)
    {
        var single = presentation is "dropdown" or "in-place" or "transition-pick" or "sprint-move"
            or "sprint-complete";
        FormField[] fields = single ? [field] : [field, Companion()];
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
            "transition-pick" => new TransitionPickTarget("A-1", [], null, []),
            "link" => new LinkFormTarget("A-1"),
            "sprint" => new SprintFormTarget(1, null),
            "sprint-start" => new StartSprintFormTarget(1, sprint, tally),
            "sprint-complete" => new CompleteSprintFormTarget(1, sprint, tally, sprint),
            "sprint-move" => new SprintMoveTarget("A-1", [sprint], null, "board"),
            _ => new EditFormTarget("A-1", new Dictionary<string, FormValue>(), false, null),
        };
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

    /// <summary>One row of the table: a field kind, its valid key script, a wrong text and the presentations it skips.</summary>
    /// <param name="Kind">The field kind.</param>
    /// <param name="Valid">The keys that give the field a valid value, split by <c>|</c>.</param>
    /// <param name="Wrong">A text typed into the field that its format refuses, or null for a kind with no format.</param>
    /// <param name="Editable">False for a kind this contract does not drive (a person field, a read-only kind).</param>
    public sealed record KindRow(FormFieldKind Kind, string Valid, string? Wrong, bool Editable)
    {
        /// <summary>The presentations that offer the kind: the in-place edit holds only the text of a pane.</summary>
        public IEnumerable<string> Presentations()
        {
            var text = Kind is FormFieldKind.Text or FormFieldKind.Paragraph;

            var choice = Kind == FormFieldKind.Select;

            return PresentationNames
                .Where(name => text || name != "in-place")
                .Where(name => Kind != FormFieldKind.Parent || !NoSearch.Contains(name))
                .Where(name => choice || !OneChoice.Contains(name));
        }

        /// <summary>The presentations whose write needs the one id of their own choice list (not run with the others).</summary>
        public IEnumerable<string> Writing()
        {
            return Presentations().Where(name => !OwnFields.Contains(name));
        }
    }

    /// <summary>What a script leaves behind: whether a write started, the error of the field and its value.</summary>
    public sealed record Outcome(bool Sent, string Error, string Value);
}
