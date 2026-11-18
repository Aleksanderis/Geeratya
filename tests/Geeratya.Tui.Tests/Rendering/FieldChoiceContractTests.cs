using System.Globalization;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// One table of scenarios over field kind x form presentation (<c>docs/internal/ui-consistency.md</c>, components 2
/// and 3; #1021): a key script goes in through the real reducer, and the phase of the form, the value chosen or
/// written, the typed filter and the footer come out. Every row runs on every presentation, so a field kind that
/// behaves differently in the create panes, the box, the Details dropdown or the transition fields fails here. A
/// presentation is added to <see cref="Navigable"/> or <see cref="OneField"/>; a field kind is added to
/// <see cref="FieldOf"/>; a scenario is one more row of <see cref="Rows"/>.
/// </summary>
/// <remarks>
/// Key script tokens: a key name (<c>Enter</c>, <c>Down</c>, <c>Esc</c>, <c>j</c>), <c>text:xyz</c> types the
/// characters, <c>reply:id=Label</c> answers the open search with that one row and <c>click:n</c> clicks row n of the
/// open list.
/// </remarks>
[Trait("Os", "Independent")]
public class FieldChoiceContractTests
{
    private const string Eve = "acc-eve";
    private const string Me = "acc-me";
    private const string Jo = "acc-jo";
    private const string Moving = "j";

    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _profile = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IssueStatus _status = new("3", "In Progress", "indeterminate");
    private static readonly IssueTypeSummary _type = new("10", "Task", 0, false);
    private static readonly string[] _navigableFooter = ["move", "choose", "field", "save", "stop editing"];
    private static readonly string[] _singleFooter = ["move", "choose", "close"];
    private static readonly string[] _noMatchNavigableFooter = ["field", "save", "clear filter"];
    private static readonly string[] _noMatchSingleFooter = ["clear filter"];
    private static readonly string[] _none = [];

    private readonly AppReducer _reducer = new(_keymap);

    /// <summary>The form phases a script can end in.</summary>
    public enum Phase
    {
        /// <summary>The focused field takes typed text.</summary>
        Editing,

        /// <summary>The form moves between fields (a form with a navigate state only).</summary>
        Navigating,

        /// <summary>The form sent its write.</summary>
        Saved,

        /// <summary>The form is closed without a write.</summary>
        Closed,
    }

    /// <summary>Presentations that have the navigate state: the field is one of several.</summary>
    public static string[] Navigable { get; } = ["box", "create", "subtask", "transition"];

    /// <summary>Presentations of one field: Enter on a chosen row writes it at once.</summary>
    public static string[] OneField { get; } = ["dropdown", "edit-box"];

    /// <summary>
    /// The scenarios. <c>AfterNavigable</c> and <c>AfterSingle</c> are the phase the script ends in for the two groups
    /// of presentations; everything else is the same in every presentation.
    /// </summary>
    public static IReadOnlyList<ContractRow> Rows { get; } =
    [
        new(
            "assignee",
            "open",
            [],
            Phase.Editing,
            Phase.Editing,
            [Eve],
            [Me, string.Empty],
            _navigableFooter,
            _singleFooter),
        new(
            "assignee",
            "quick-me",
            ["Down", "Enter"],
            Phase.Navigating,
            Phase.Saved,
            [Me],
            null,
            null,
            null),
        new(
            "assignee",
            "click-a-row",
            ["click:0"],
            Phase.Navigating,
            Phase.Saved,
            [Me],
            null,
            null,
            null),
        new(
            "assignee",
            "search-result",
            ["text:jo", "reply:acc-jo=Jo Smith", "Enter"],
            Phase.Navigating,
            Phase.Saved,
            [Jo],
            null,
            null,
            null),
        new(
            "assignee",
            "clear",
            ["Down", "Down", "Enter"],
            Phase.Navigating,
            Phase.Saved,
            _none,
            null,
            null,
            null),
        new(
            "assignee",
            "escape-clears-the-filter",
            ["text:te", "Esc"],
            Phase.Editing,
            Phase.Editing,
            [Eve],
            [Me, string.Empty],
            _navigableFooter,
            _singleFooter),
        new(
            "assignee",
            "escape-twice-leaves",
            ["text:te", "Esc", "Esc"],
            Phase.Navigating,
            Phase.Closed,
            [Eve],
            null,
            null,
            null),
        new(
            "assignee",
            "jk-after-a-choice",
            ["Down", "Enter", Moving],
            Phase.Navigating,
            Phase.Saved,
            [Me],
            null,
            null,
            null),
        new(
            "reporter",
            "open",
            [],
            Phase.Editing,
            Phase.Editing,
            [Eve],
            [Me],
            _navigableFooter,
            _singleFooter),
        new(
            "reporter",
            "quick-me",
            ["Down", "Enter"],
            Phase.Navigating,
            Phase.Saved,
            [Me],
            null,
            null,
            null),
        new(
            "reporter",
            "search-result",
            ["text:jo", "reply:acc-jo=Jo Smith", "Enter"],
            Phase.Navigating,
            Phase.Saved,
            [Jo],
            null,
            null,
            null),
        new(
            "reporter",
            "escape-clears-the-filter",
            ["text:te", "Esc"],
            Phase.Editing,
            Phase.Editing,
            [Eve],
            [Me],
            _navigableFooter,
            _singleFooter),
        new(
            "reporter",
            "jk-after-a-choice",
            ["Down", "Enter", Moving],
            Phase.Navigating,
            Phase.Saved,
            [Me],
            null,
            null,
            null),
        new(
            "priority",
            "no-match",
            ["text:zz"],
            Phase.Editing,
            Phase.Editing,
            _none,
            [],
            _noMatchNavigableFooter,
            _noMatchSingleFooter),
        new(
            "parent",
            "search-result",
            ["text:A", "reply:A-9=A-9 Epic", "Enter"],
            Phase.Navigating,
            Phase.Saved,
            ["A-9"],
            null,
            null,
            null),
        new(
            "parent",
            "click-a-row",
            ["text:A", "reply:A-9=A-9 Epic", "click:0"],
            Phase.Navigating,
            Phase.Saved,
            ["A-9"],
            null,
            null,
            null),
        new(
            "parent",
            "jk-after-a-choice",
            ["text:A", "reply:A-9=A-9 Epic", "Enter", Moving],
            Phase.Navigating,
            Phase.Saved,
            ["A-9"],
            null,
            null,
            null),
        new(
            "priority",
            "choose",
            ["Enter"],
            Phase.Navigating,
            Phase.Saved,
            ["1"],
            null,
            null,
            null),
        new(
            "priority",
            "click-a-row",
            ["click:1"],
            Phase.Navigating,
            Phase.Saved,
            ["2"],
            null,
            null,
            null),
        new(
            "priority",
            "jk-after-a-choice",
            ["Enter", Moving],
            Phase.Navigating,
            Phase.Saved,
            ["1"],
            null,
            null,
            null),
        new(
            "labels",
            "take-a-label-keeps-editing",
            ["Down", "Enter"],
            Phase.Editing,
            Phase.Saved,
            ["frontend"],
            null,
            null,
            null),
        new(
            "labels",
            "click-a-label-keeps-editing",
            ["click:0"],
            Phase.Editing,
            Phase.Saved,
            ["frontend"],
            null,
            null,
            null),
        new(
            "labels",
            "escape-clears-the-filter",
            ["text:te", "Esc"],
            Phase.Editing,
            Phase.Editing,
            ["backend", "frontend"],
            null,
            null,
            null),
        new(
            "labels",
            "escape-twice-leaves",
            ["text:te", "Esc", "Esc"],
            Phase.Navigating,
            Phase.Closed,
            ["backend", "frontend"],
            null,
            null,
            null),
        new(
            "components",
            "click-a-value-keeps-editing",
            ["click:0"],
            Phase.Editing,
            Phase.Saved,
            ["a"],
            null,
            null,
            null),
        new(
            "components",
            "take-a-value-keeps-editing",
            ["Down", "Enter"],
            Phase.Editing,
            Phase.Saved,
            ["a"],
            null,
            null,
            null),
    ];

    public static TheoryData<string, string, string> Cases()
    {
        return CasesOf(static _ => true);
    }

    public static TheoryData<string, string, string> JkCases()
    {
        return CasesOf(static row => row.Scenario.StartsWith("jk-", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void GivenAScenario_WhenItsKeysArePressed_ThenEveryPresentationEndsTheSame(
        string field,
        string scenario,
        string presentation)
    {
        var row = Rows.Single(candidate => candidate.Field == field && candidate.Scenario == scenario);
        var navigable = Navigable.Contains(presentation);

        var outcome = Run(presentation, row);

        Assert.Equal(navigable ? row.AfterNavigable : row.AfterSingle, outcome.Phase);

        // The typed filter stays while nothing matches it; every other script ends with an empty one.
        Assert.Equal(row.Scenario == "no-match" ? "zz" : string.Empty, outcome.Entry);

        if (outcome.Phase != Phase.Closed)
        {
            Assert.Equal(row.Chosen, outcome.Chosen);
        }

        if (row.PopupRowIds is { } popupRowIds)
        {
            Assert.Equal(popupRowIds, outcome.PopupRowIds);
        }

        if (row.FooterNavigable is not null)
        {
            Assert.Equal(navigable ? row.FooterNavigable : row.FooterSingle, outcome.Footer);

            // A filter that matches nothing leaves no row to choose: the status line says so by leaving Enter out.
            var hasRows = outcome.PopupRowIds.Length > 0;

            Assert.Equal(hasRows, outcome.DrawnFooter.Contains("Enter choose", StringComparison.Ordinal));
            Assert.Contains("Esc ", outcome.DrawnFooter, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(JkCases))]
    public void GivenAChoice_WhenJFollows_ThenItMovesBetweenFieldsAndNeverTypesIntoThePicker(
        string field,
        string scenario,
        string presentation)
    {
        var row = Rows.Single(candidate => candidate.Field == field && candidate.Scenario == scenario);

        var outcome = Run(presentation, row);

        Assert.Equal(string.Empty, outcome.Entry);

        if (Navigable.Contains(presentation))
        {
            Assert.NotEqual(outcome.PickerIndex, outcome.FocusAfterKey);
            Assert.Equal(Phase.Navigating, outcome.Phase);
        }
        else
        {
            Assert.Equal(Phase.Saved, outcome.Phase);
        }
    }

    [Theory]
    [MemberData(nameof(Presentations))]
    public void GivenAReporterWithNoRowsToOffer_WhenDrawn_ThenEveryPresentationSaysTypeToSearch(string presentation)
    {
        var field = FieldOf("reporter").WithInitial(FormValue.OfChosen([Me]));
        var state = Open(presentation, field);

        var lines = FrameBuilder.Build(state, 120, 36, _keymap);

        Assert.Contains(lines, line => line.Contains(FieldRows.TypeToSearchText, StringComparison.Ordinal));
    }

    public static TheoryData<string> Presentations()
    {
        return [.. Navigable.Concat(OneField)];
    }

    private static TheoryData<string, string, string> CasesOf(Func<ContractRow, bool> selected)
    {
        var cases = new TheoryData<string, string, string>();

        foreach (var row in Rows.Where(selected))
        {
            foreach (var presentation in Navigable.Concat(OneField))
            {
                cases.Add(row.Field, row.Scenario, presentation);
            }
        }

        return cases;
    }

    private static FormField FieldOf(string name)
    {
        return name switch
        {
            "assignee" => FormField.Create(FormFieldIds.Assignee, "Assignee", FormFieldKind.User, false, [])
                .WithInitial(FormValue.OfChosen([Eve])),
            "reporter" => FormField.Create(FormFieldIds.Reporter, "Reporter", FormFieldKind.User, true, [])
                .WithInitial(FormValue.OfChosen([Eve])),
            "parent" => FormField.Create(FormFieldIds.Parent, "Parent", FormFieldKind.Parent, false, []),
            "labels" => FormField.Create(FormFieldIds.Labels, "Labels", FormFieldKind.Labels, false, [])
                .WithInitial(FormValue.OfChosen(["backend", "frontend"])),
            "components" => FormField.Create(
                "components",
                "Components",
                FormFieldKind.MultiSelect,
                false,
                [new FieldOption("a", "Alpha"), new FieldOption("b", "Beta")]),
            _ => FormField.Create(
                FormFieldIds.Priority,
                "Priority",
                FormFieldKind.Select,
                true,
                [new FieldOption("1", "High"), new FieldOption("2", "Low")]),
        };
    }

    private static TransitionChoice Choice()
    {
        return new TransitionChoice("41", "Done", new IssueStatus("3", "Done", "done"), true, true, []);
    }

    private static FormTarget NavigableTarget(string presentation)
    {
        return presentation switch
        {
            "box" => new AssignFormTarget("GCM-12", null),
            "create" => new CreateFormTarget("GCM", null, false, new CreateIssueType("10", "Task", "", 0, false)),
            "subtask" => new CreateFormTarget("GCM", "GCM-12", true, new CreateIssueType("11", "Sub", "", -1, true)),
            _ => new TransitionFormTarget("GCM-12", Choice()),
        };
    }

    private static string[] FooterOf(AppState state)
    {
        var form = state.Session.Tabs[0].Form;

        if (form is null || FormFooter.PopupChips(form, _keymap) is not { } chips)
        {
            return _none;
        }

        return [.. chips.OfType<HintChip>().Select(static chip => chip.Label)];
    }

    private static string[] RowsOf(AppState state)
    {
        if (state.Session.Tabs[0].Form?.Suggest is not { } popup)
        {
            return _none;
        }

        return [.. popup.Items.Select(static item => item.Id)];
    }

    private static string[] WrittenChoice(WriteRequest request)
    {
        return request switch
        {
            EditRequest edit => [.. edit.Changes[0].Value.Chosen],
            AssignRequest { Assignee: { } assignee } => [assignee.AccountId],
            _ => _none,
        };
    }

    private FieldOutcome Run(string presentation, ContractRow row)
    {
        var field = FieldOf(row.Field);
        var state = Open(presentation, field);
        var pickerIndex = state.Session.Tabs[0].Form!.Focus;
        var written = new List<WriteRequest>();
        var focusAfterKey = pickerIndex;

        foreach (var token in row.Keys)
        {
            state = Apply(state, token, written);

            if (token == Moving && state.Session.Tabs[0].Form is { } moved)
            {
                focusAfterKey = moved.Focus;
            }
        }

        var form = state.Session.Tabs[0].Form;
        var phase = PhaseOf(form, written);
        var chosen = written.Count > 0 ? WrittenChoice(written[0]) : ChosenOf(form, pickerIndex);
        var entry = form is { Fields.Count: > 0 } ? FieldAt(form, pickerIndex).Entry : string.Empty;

        var drawn = FrameBuilder.Build(state, 120, 36, _keymap);

        return new FieldOutcome(
            phase,
            chosen,
            entry,
            RowsOf(state),
            FooterOf(state),
            drawn[^1],
            pickerIndex,
            focusAfterKey);
    }

    private static Phase PhaseOf(FormState? form, List<WriteRequest> written)
    {
        if (written.Count > 0)
        {
            return Phase.Saved;
        }

        if (form is null)
        {
            return Phase.Closed;
        }

        return form.Navigating ? Phase.Navigating : Phase.Editing;
    }

    private static string[] ChosenOf(FormState? form, int index)
    {
        if (form is not { Fields.Count: > 0 })
        {
            return _none;
        }

        return [.. FieldAt(form, index).Value.Chosen];
    }

    private static FormField FieldAt(FormState form, int index)
    {
        return form.Fields[Math.Min(index, form.Fields.Count - 1)];
    }

    private AppState Apply(AppState state, string token, List<WriteRequest> written)
    {
        if (token.StartsWith("text:", StringComparison.Ordinal))
        {
            foreach (var character in token["text:".Length..])
            {
                state = Press(state, $"{character}", written);
            }

            return state;
        }

        if (token.StartsWith("reply:", StringComparison.Ordinal))
        {
            var parts = token["reply:".Length..].Split('=');
            var form = state.Session.Tabs[0].Form!;
            var item = new Suggestion(parts[0], parts[1], null, false);
            var loaded = new SuggestionsLoaded(state.Session.Tabs[0].Id, form.Suggest!.RequestId, [item]);

            return _reducer.Reduce(state, loaded).State;
        }

        if (token.StartsWith("click:", StringComparison.Ordinal))
        {
            var index = int.Parse(token["click:".Length..], CultureInfo.InvariantCulture);
            var clicked = new OverlayRowClicked(MouseTarget.FormChoice, index, string.Empty, _now);
            var result = _reducer.Reduce(state, clicked);

            written.AddRange(result.Effects.OfType<ExecuteWriteEffect>().Select(static effect => effect.Request));

            return result.State;
        }

        return Press(state, token, written);
    }

    private AppState Press(AppState state, string key, List<WriteRequest> written)
    {
        var result = _reducer.Reduce(state, new KeyPressed(key, _now));

        written.AddRange(result.Effects.OfType<ExecuteWriteEffect>().Select(static effect => effect.Request));

        return result.State;
    }

    // The field sits between two text fields, focused and being edited, as after a Tab.
    internal AppState Open(string presentation, FormField field)
    {
        if (Navigable.Contains(presentation))
        {
            return OpenNavigable(presentation, field);
        }

        return OpenSingle(presentation, field);
    }

    private AppState OpenNavigable(string presentation, FormField field)
    {
        var state = Loaded();
        var tab = state.Session.Tabs[0];
        var other = FormField.Create("summary", "Summary", FormFieldKind.Text, false, []);
        var form = FormState.Opened(NavigableTarget(presentation), 7, null);
        var withForm = state with
        {
            Session = state.Session with { Tabs = [tab with { Form = form }] },
            Mode = InputMode.Insert,
        };
        var tail = FormField.Create("duedate", "Due date", FormFieldKind.Text, false, []);
        var loaded = _reducer.Reduce(withForm, new FormMetadataLoaded(tab.Id, 7, [other, field, tail])).State;

        return _reducer.Reduce(loaded, new KeyPressed("Tab", _now)).State;
    }

    private AppState OpenSingle(string presentation, FormField field)
    {
        var state = Loaded();
        var tab = state.Session.Tabs[0];

        if (presentation == "dropdown")
        {
            state = _reducer.Reduce(state, new IssueEditableLoaded(tab.Id, "GCM-12", [DetailFieldIds.Priority])).State;

            foreach (var key in new[] { "Ctrl-l", "j", "Enter" })
            {
                state = _reducer.Reduce(state, new KeyPressed(key, _now)).State;
            }

            var opened = state.Session.Tabs[0].Form!;

            return _reducer.Reduce(state, new FormMetadataLoaded(tab.Id, opened.RequestId, [field])).State;
        }

        var target = new EditFormTarget("GCM-12", new Dictionary<string, FormValue>(), false, null, field.Id);
        var form = FormState.Opened(target, 7, null);
        var withForm = state with
        {
            Session = state.Session with { Tabs = [tab with { Form = form }] },
            Mode = InputMode.Insert,
        };

        return _reducer.Reduce(withForm, new FormMetadataLoaded(tab.Id, 7, [field])).State;
    }

    private AppState Loaded()
    {
        var state = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_profile),
            Session = SiteSession.Empty(_profile) with { Connection = new Connected("Me", Me) },
        };

        state = _reducer.Reduce(state, new ViewportChanged(
            36 - 1 - FrameBuilder.HeaderRows - ListLayout.HeaderRows - ListLayout.FooterRows,
            120,
            FrameBuilder.BodyHeight(36))).State;

        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = GCM", ViewLayout.List, "GCM");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition));
        var tab = opened.State.Session.Tabs[0];
        var row = new IssueSummary("1", "GCM-12", "Fix login", _status, _type, null, null, [], null, null, "GCM");
        var page = new IssuePage([row], null, null);
        var listed = _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
        var shown = _reducer.Reduce(listed, new KeyPressed("Enter")).State;
        var detail = shown.Session.Tabs[0].Detail[0];

        return _reducer.Reduce(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(), _now)).State;
    }

    private static IssueDetail Issue()
    {
        return new IssueDetail(
            "1",
            "GCM-12",
            "Fix login",
            _status,
            _type,
            "High",
            new IssueAssignee("j", "Jane Doe"),
            new IssueAssignee("o", "Ola Nordmann"),
            ["auth"],
            _now.AddDays(-9),
            _now.AddHours(-2),
            "GCM",
            null,
            null,
            [],
            [],
            []);
    }

    /// <summary>One scenario of the table: a field kind, a key script and what every presentation ends in.</summary>
    /// <param name="Field">The field kind (<c>assignee</c>, <c>reporter</c>, <c>parent</c>, <c>priority</c>).</param>
    /// <param name="Scenario">The name of the scenario.</param>
    /// <param name="Keys">The key script.</param>
    /// <param name="AfterNavigable">The phase in a form with the navigate state.</param>
    /// <param name="AfterSingle">The phase in a form of one field.</param>
    /// <param name="Chosen">The value the field holds or the write carries when the script ends.</param>
    /// <param name="PopupRowIds">The ids of the popup rows when the script ends, or null when the row does not check
    /// them.</param>
    /// <param name="FooterNavigable">The popup footer labels in a navigable form, or null to skip the check.</param>
    /// <param name="FooterSingle">The popup footer labels in a form of one field.</param>
    public sealed record ContractRow(
        string Field,
        string Scenario,
        string[] Keys,
        Phase AfterNavigable,
        Phase AfterSingle,
        string[] Chosen,
        string[]? PopupRowIds,
        string[]? FooterNavigable,
        string[]? FooterSingle);

    private sealed record FieldOutcome(
        Phase Phase,
        string[] Chosen,
        string Entry,
        string[] PopupRowIds,
        string[] Footer,
        string DrawnFooter,
        int PickerIndex,
        int FocusAfterKey);
}
