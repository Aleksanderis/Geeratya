using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>
/// One way to open a field (<c>docs/internal/ui-consistency.md</c>, component 3): every editable field of the Details
/// pane opens as a dropdown under its block, and the same field from a list key or the palette opens the same form
/// (the detail first, then the dropdown), whatever the route.
/// </summary>
[Trait("Os", "Independent")]
public class DetailFieldOpeningTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Theory]
    [InlineData(DetailFieldIds.Status)]
    [InlineData(DetailFieldIds.Priority)]
    [InlineData(DetailFieldIds.Assignee)]
    [InlineData(DetailFieldIds.Reporter)]
    [InlineData(DetailFieldIds.Labels)]
    [InlineData(DetailFieldIds.Estimate)]
    public void GivenAnEditableField_WhenEnterIsPressedOnIt_ThenItOpensAsADropdownUnderTheField(string field)
    {
        var state = OnField(Opened(), field);

        var result = Press(state, "Enter");

        var form = Active(result.State).Form;
        Assert.NotNull(form);
        Assert.Equal(field, form.Dropdown);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Theory]
    [InlineData("Space i a", DetailFieldIds.Assignee)]
    [InlineData("Space i s", DetailFieldIds.Status)]
    public void GivenAListKey_WhenPressed_ThenTheDetailOpensAndTheSameDropdownHangsUnderTheField(
        string keys,
        string field)
    {
        var fromList = Keys(Listed(), keys.Split(' '));
        var fromDetails = Press(OnField(Opened(), field), "Enter");

        var list = Active(fromList);
        var details = Active(fromDetails.State);
        Assert.Single(list.Detail);
        Assert.Equal(field, list.Form!.Dropdown);
        Assert.Equal(details.Form!.Dropdown, list.Form.Dropdown);
        Assert.Equal(details.Form.Target.GetType(), list.Form.Target.GetType());
        Assert.Equal(details.Form.Target.Title, list.Form.Target.Title);
    }

    [Theory]
    [InlineData("priority", DetailFieldIds.Priority)]
    [InlineData("labels", DetailFieldIds.Labels)]
    [InlineData("estimate", DetailFieldIds.Estimate)]
    [InlineData("assign", DetailFieldIds.Assignee)]
    [InlineData("status", DetailFieldIds.Status)]
    public void GivenAPaletteCommand_WhenRun_ThenTheDetailOpensAndTheSameDropdownHangsUnderTheField(
        string command,
        string field)
    {
        var opened = _reducer.Reduce(
            Listed() with { Mode = InputMode.Command, PaletteText = command },
            new KeyPressed("Enter"));

        var tab = Active(opened.State);
        Assert.Single(tab.Detail);
        Assert.Equal(field, tab.Form!.Dropdown);
    }

    private AppState OnField(AppState state, string field)
    {
        var side = Press(state, "Ctrl-l").State;

        for (var step = 0; step < 30 && SideTarget(side) != field; step++)
        {
            side = Press(side, "j").State;
        }

        Assert.Equal(field, SideTarget(side));

        return side;
    }

    private static string? SideTarget(AppState state)
    {
        var detail = Active(state).Detail[^1];

        return detail.Document!.Side!.Targets[detail.SideCursor!.Value]?.Value;
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private AppState Keys(AppState state, string[] keys)
    {
        return Keys(new ReduceResult(state, []), keys).State;
    }

    private ReduceResult Keys(ReduceResult start, string[] keys)
    {
        var result = start;

        foreach (var key in keys)
        {
            result = Press(result.State, key);
        }

        return result;
    }

    private AppState Opened()
    {
        var shown = Press(Listed(), "Enter").State;
        var detail = Active(shown).Detail[0];
        var loaded = _reducer.Reduce(
            shown,
            new IssueDetailLoaded(Active(shown).Id, detail.IssueRequest, Issue() with { Estimate = 3 }, _now)).State;
        string[] editable = [DetailFieldIds.Priority, DetailFieldIds.Assignee, DetailFieldIds.Labels, DetailFieldIds.Reporter];

        return _reducer.Reduce(loaded, new IssueEditableLoaded(Active(loaded).Id, "A-1", editable)).State;
    }

    private AppState Listed()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var sized = _reducer.Reduce(start, new ViewportChanged(30, 120)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");

        return _reducer.Reduce(
            opened.State,
            new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([row], null, null), _now)).State;
    }

    private static IssueDetail Issue()
    {
        return new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            _story,
            "Medium",
            new IssueAssignee("acc-eve", "Eve Example"),
            new IssueAssignee("acc-ola", "Ola Nordmann"),
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
