using System.Globalization;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Writes;

[Trait("Os", "Independent")]
public class BulkWriteMergeTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly IssueSprint _sprint = new(8, "Sprint 8", SprintState.Future);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenBulkMove_WhenStarted_ThenOnlyTheNamedRowsChangeIgnoringKeyCase()
    {
        var state = Loaded(20);

        var started = Start(state, ["a-3", "A-7", "A-20"]);

        var rows = started.Session.Tabs[0].Rows;
        Assert.Equal(["A-3", "A-7", "A-20"], rows.Where(row => row.Sprints.Count == 1).Select(row => row.Key));
        Assert.Equal(3, Assert.Single(started.Session.Operations).Undo.Count);
    }

    [Fact]
    public void GivenBulkMove_WhenItFails_ThenEveryRowIsRestored()
    {
        var state = Loaded(20);
        var started = Start(state, ["A-3", "A-7", "A-20"]);
        var operation = Assert.Single(started.Session.Operations);
        var failure = new WriteError(WriteErrorKind.Validation, 400, "no", [], []);

        var failed = _reducer.Reduce(started, new WriteFailed(operation.Id, _now, failure)).State;

        Assert.All(failed.Session.Tabs[0].Rows, row => Assert.Empty(row.Sprints));
        Assert.Empty(failed.Session.Operations);
    }

    [Fact]
    public void GivenLoadWhileBulkMoveRuns_WhenMerging_ThenHeldRowsKeepTheirValueAndOthersComeFromTheReply()
    {
        var state = Loaded(20);
        var started = Start(state, ["A-3", "A-7"]);
        var incoming = Rows(20).Select(row => row with { Summary = "fresh" }).ToArray();

        var merged = WriteMerge.KeepHeldRows(started, started.Session.Tabs[0], incoming);

        Assert.Equal(["A-3", "A-7"], merged.Where(row => row.Sprints.Count == 1).Select(row => row.Key));
        Assert.Equal(18, merged.Count(row => row.Summary == "fresh"));
    }

    [Fact]
    public void GivenEditOfASubtaskStub_WhenStartedAndFailed_ThenTheStubSummaryChangesAndIsRestored()
    {
        var state = WithStub(Loaded(5));
        var change = new FieldChange(FormFieldIds.Summary, FormFieldKind.Text, FormValue.OfText("New title"));
        var request = new EditRequest("A-99", [change]);

        var started = _reducer.Reduce(state, new WriteRequested(new TabId(1), request, _now)).State;

        Assert.Equal("New title", started.Session.Tabs[0].Rows[0].Subtasks[0].Summary);

        var operation = Assert.Single(started.Session.Operations);
        var failure = new WriteError(WriteErrorKind.Validation, 400, "no", [], []);
        var failed = _reducer.Reduce(started, new WriteFailed(operation.Id, _now, failure)).State;

        Assert.Equal("Sub-task", failed.Session.Tabs[0].Rows[0].Subtasks[0].Summary);
    }

    [Fact]
    public void GivenParentAndItsStubBothInTheReply_WhenSettled_ThenTheRowAndTheStubAreBothReplaced()
    {
        var state = Start(WithStub(Loaded(5)), ["A-1", "A-99"]);
        var operation = Assert.Single(state.Session.Operations);
        var parent = state.Session.Tabs[0].Rows[0] with { Summary = "Fresh parent" };
        var listed = Rows(1)[0] with { Key = "A-99", Summary = "Fresh stub" };

        var settled = _reducer.Reduce(state, new WriteSucceeded(operation.Id, _now, [parent, listed])).State;

        var row = settled.Session.Tabs[0].Rows[0];
        Assert.Equal("Fresh parent", row.Summary);
        Assert.Equal("Fresh stub", row.Subtasks[0].Summary);
    }

    [Fact]
    public void GivenTwoChangesOnOneKey_WhenStartedAndFailed_ThenBothAreKeptAndRolledBack()
    {
        var state = Loaded(5);

        var started = Start(state, ["A-2", "a-2"]);

        var operation = Assert.Single(started.Session.Operations);
        Assert.Equal(2, operation.Undo.Count);
        Assert.Equal([_sprint], started.Session.Tabs[0].Rows[1].Sprints);

        var failure = new WriteError(WriteErrorKind.Validation, 400, "no", [], []);
        var failed = _reducer.Reduce(started, new WriteFailed(operation.Id, _now, failure)).State;

        Assert.All(failed.Session.Tabs[0].Rows, row => Assert.Empty(row.Sprints));
    }

    [Fact]
    public void GivenIssueInTwoTabs_WhenPlanned_ThenTheFirstTabsRowDecidesTheUndo()
    {
        var state = Loaded(3);
        var first = state.Session.Tabs[0];
        var older = new IssueSprint(5, "Sprint 5", SprintState.Future);
        var secondRows = first.Rows.Select(row => row with { Sprints = [older] }).ToArray();
        var second = first with { Id = new TabId(2), Rows = secondRows };
        var both = state with { Session = state.Session with { Tabs = [first, second] } };

        var started = Start(both, ["A-1"]);

        var change = Assert.IsType<SprintRowChange>(Assert.Single(Assert.Single(started.Session.Operations).Undo));
        Assert.Empty(change.Before);
        Assert.Equal([_sprint], started.Session.Tabs[0].Rows[0].Sprints);
        Assert.Equal([_sprint], started.Session.Tabs[1].Rows[0].Sprints);
    }

    [Fact]
    public void GivenTwoOperationsRunning_WhenWriteHitsOneKey_ThenBusyTextNamesThatOperation()
    {
        var change = new FieldChange(FormFieldIds.Summary, FormFieldKind.Text, FormValue.OfText("New title"));
        var edit = new WriteRequested(new TabId(1), new EditRequest("A-1", [change]), _now);
        var editing = _reducer.Reduce(Loaded(5), edit).State;
        var running = Start(editing, ["A-2", "A-3"]);

        var blockedOnMove = Start(running, ["A-5", "a-3"]);
        var blockedOnEdit = Start(running, ["A-4", "A-1"]);
        var blockedOnBoth = Start(running, ["A-3", "A-1"]);

        Assert.Equal("a-3 is busy (moving)", blockedOnMove.Message);
        Assert.Equal("A-1 is busy (editing)", blockedOnEdit.Message);
        Assert.Equal("A-1 is busy (editing)", blockedOnBoth.Message);
        Assert.Equal(2, blockedOnMove.Session.Operations.Count);
        Assert.Equal(2, blockedOnEdit.Session.Operations.Count);
    }

    private static AppState WithStub(AppState state)
    {
        var tab = state.Session.Tabs[0];
        var stub = new IssueSubtask("A-99", "Sub-task", new IssueStatus("1", "To Do", "new"), "Sub-task");
        var rows = tab.Rows.ToArray();

        rows[0] = rows[0] with { Subtasks = [stub] };

        return state with { Session = state.Session with { Tabs = [tab with { Rows = rows }] } };
    }

    private AppState Start(AppState state, string[] keys)
    {
        var request = new SprintMoveRequest(keys, _sprint, "the backlog");

        return _reducer.Reduce(state, new WriteRequested(new TabId(1), request, _now, true)).State;
    }

    private static List<IssueSummary> Rows(int count)
    {
        return [.. Enumerable.Range(1, count).Select(number => new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
            $"A-{number}",
            $"Summary {number}",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A"))];
    }

    private static AppState Loaded(int count)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "View");
        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 20) with
        {
            Load = LoadState.Idle,
            Rows = Rows(count),
            SelectedKey = "A-1",
        };
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }
}
