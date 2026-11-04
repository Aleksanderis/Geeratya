using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>The fields of the issue detail as a form: one cursor stop per field, Enter edits it (#678).</summary>
[Trait("Os", "Independent")]
public class IssueDetailFieldTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueAssignee _reporter = new("acc-ola", "Ola Nordmann");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAWrappedLabelsValue_WhenMovingDown_ThenTheFieldIsOneStopAndTheNextFieldFollows()
    {
        var labels = Enumerable.Range(1, 12).Select(number => $"label-number-{number}").ToArray();
        var state = Opened(Issue() with { Labels = labels });
        var document = Document(state);
        var labelsLine = document.Targets.ToList().FindIndex(IsField(DetailFieldIds.Labels));
        var block = document.BlockAt(labelsLine);

        Assert.NotNull(block);
        Assert.True(block.End - block.Start > 1);

        var onLabels = OnTarget(state, DetailFieldIds.Labels);
        var next = Press(onLabels, "j").State;

        Assert.Equal(block.Start, Top(onLabels).Cursor);
        Assert.Equal(block.End, Top(next).Cursor);
        Assert.Equal(DetailFieldIds.Updated, Document(next).Targets[Top(next).Cursor]?.Value);
    }

    [Fact]
    public void GivenALineInsideAWrappedField_WhenSnapping_ThenTheCursorLandsOnTheFieldStart()
    {
        var labels = Enumerable.Range(1, 12).Select(number => $"label-number-{number}").ToArray();
        var document = Document(Opened(Issue() with { Labels = labels }));
        var block = document.BlockAt(document.Targets.ToList().FindIndex(IsField(DetailFieldIds.Labels)))!;

        Assert.Equal(block.Start, document.Snap(block.End - 1));
    }

    [Fact]
    public void GivenFocusOnTheSummary_WhenMovingDown_ThenTheCursorSkipsTheGapAndLandsOnStatus()
    {
        var state = Opened(Issue());

        Assert.Equal(DetailFieldIds.Summary, Document(state).Targets[Top(state).Cursor]?.Value);

        var next = Press(state, "j").State;

        Assert.Equal(DetailFieldIds.Status, Document(next).Targets[Top(next).Cursor]?.Value);
    }

    [Fact]
    public void GivenFocusOnStatus_WhenPressingEnter_ThenTheTransitionPickerLoads()
    {
        var result = Press(OnTarget(Opened(Issue()), DetailFieldIds.Status), "Enter");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.IsType<TransitionPickTarget>(effect.Target);
    }

    [Fact]
    public void GivenFocusOnAssignee_WhenPressingEnter_ThenThePeoplePickerLoads()
    {
        var result = Press(OnTarget(Opened(Issue()), DetailFieldIds.Assignee), "Enter");

        Assert.Equal("Assign A-1", Active(result.State).Form!.Target.Title);
    }

    [Theory]
    [InlineData(DetailFieldIds.Priority)]
    [InlineData(DetailFieldIds.Labels)]
    [InlineData(DetailFieldIds.Summary)]
    public void GivenFocusOnAnEditableField_WhenPressingEnter_ThenTheEditFormIsLimitedToIt(string field)
    {
        var result = Press(OnTarget(Opened(Issue()), field), "Enter");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal(field, Assert.IsType<EditFormTarget>(effect.Target).OnlyId);
    }

    [Fact]
    public void GivenFocusOnTheDescription_WhenPressingEnter_ThenTheEditFormIsLimitedToTheDescription()
    {
        var state = OnTarget(Opened(Issue()), DetailFieldIds.Description);

        var result = Press(state, "Enter");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal(FormFieldIds.Description, Assert.IsType<EditFormTarget>(effect.Target).OnlyId);
    }

    [Fact]
    public void GivenFocusOnTheParent_WhenPressingEnter_ThenTheParentIssueOpens()
    {
        var parent = new IssueStub("A-0", "Onboarding", _todo, _story);
        var state = OnTarget(Opened(Issue() with { Parent = parent }), DetailFieldIds.Parent);

        var result = Press(state, "Enter");

        Assert.Equal("A-0", Assert.Single(result.Effects.OfType<LoadIssueDetailEffect>()).Key);
    }

    [Fact]
    public void GivenFocusOnATallField_WhenPagingDown_ThenTheCursorLeavesTheField()
    {
        var labels = Enumerable.Range(1, 60).Select(number => $"label-number-{number}").ToArray();
        var state = OnTarget(Opened(Issue() with { Labels = labels }), DetailFieldIds.Labels);
        var start = Top(state).Cursor;

        var next = Press(state, "Ctrl-d").State;

        Assert.True(Top(next).Cursor > start);
        Assert.NotEqual(DetailFieldIds.Labels, Document(next).Targets[Top(next).Cursor]?.Value);
    }

    [Fact]
    public void GivenFocusOnAReadOnlyField_WhenPressingEnter_ThenTheStatusLineSaysSoAndNothingLoads()
    {
        var result = Press(OnTarget(Opened(Issue()), DetailFieldIds.Reporter), "Enter");

        Assert.Equal("Reporter is read-only here.", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAnIssueWithoutAssigneeOrLabels_WhenBuilding_ThenTheEditableFieldsStillHaveAStop()
    {
        var state = Opened(Issue() with { Assignee = null, Labels = [], Priority = null });

        Assert.Contains(Document(state).Targets, IsField(DetailFieldIds.Assignee));
        Assert.Contains(Document(state).Targets, IsField(DetailFieldIds.Labels));
        Assert.Contains(Document(state).Targets, IsField(DetailFieldIds.Priority));
    }

    [Fact]
    public void GivenFocusOnAField_WhenOpeningInTheBrowser_ThenTheIssuePageOpens()
    {
        var result = Press(OnTarget(Opened(Issue()), DetailFieldIds.Priority), "Space", "o");

        Assert.Contains(result.Effects, effect => effect is OpenUrlEffect);
    }

    private static Predicate<DetailTarget?> IsField(string id)
    {
        return target => target is { Kind: DetailTargetKind.Field } && target.Value == id;
    }

    private AppState OnTarget(AppState state, string id)
    {
        var steps = 0;

        while (!IsField(id)(Document(state).Targets[Top(state).Cursor]) && steps++ < Document(state).Lines.Count)
        {
            state = Press(state, "j").State;
        }

        Assert.True(IsField(id)(Document(state).Targets[Top(state).Cursor]), $"no field {id}");

        return state;
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static DetailState Top(AppState state)
    {
        return Active(state).Detail[^1];
    }

    private static DetailDocument Document(AppState state)
    {
        return Top(state).Document!;
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key));
        }

        return result;
    }

    private AppState Opened(IssueDetail issue)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var sized = _reducer.Reduce(start, new ViewportChanged(30, 100)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");
        var listed = _reducer.Reduce(
            opened.State,
            new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([row], null, null), _now)).State;
        var shown = Press(listed, "Enter").State;
        var detail = Active(shown).Detail[0];

        return _reducer.Reduce(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, issue, _now)).State;
    }

    private static IssueDetail Issue()
    {
        return new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            _story,
            "High",
            new IssueAssignee("acc-eve", "Eve Example"),
            _reporter,
            ["backend"],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            null,
            null,
            [],
            [],
            []);
    }
}
