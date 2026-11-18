using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Tui.Tests.Rendering;

// The places of the contract: each one reaches its dialog through the reducer, as a user does.
public partial class ConfirmContractTests
{
    private static AppState Asked(string place)
    {
        return place switch
        {
            "new issue" => Discard(OpenCreate()),
            "edit form" => Discard(OpenBox()),
            "comment editor" => Discard(CommentEditorOverlayTests.StateWith(
                CommentEditorOverlayTests.Editor("hello"))),
            "profile form" => Discard(AppState.Initial with
            {
                Mode = InputMode.Insert,
                Form = ProfileForm.Start with { Name = "work" },
            }),
            "remove profile" => Press(PaletteWith("profile remove dev"), "Enter"),
            "switch profile" => Press(PaletteWith("profile switch dev"), "Enter"),
            "quit" => Press(PaletteWith("quit"), "Enter"),
            "delete issue" => DeleteIssue(),
            "delete comment" => Press(OnComment(Detail()), "Space", "m", "d"),
            "unlink" => Press(OnLink(Detail(), "2"), "Space", "i", "u"),
            "status" => Write(new TransitionRequest("A-1", Choice(), [], true)),
            "rank across sections" => Write(new RankMoveRequest(
                ["A-1", "A-3"],
                "A-2",
                true,
                null,
                ["A-1", "A-2", "A-3"],
                new IssueSprint(8, "Sprint 2", SprintState.Future),
                "the backlog")),
            "sprint move" => Write(new SprintMoveRequest(
                ["A-1", "A-2"],
                new IssueSprint(8, "Sprint 2", SprintState.Future),
                "the backlog")),
            "complete sprint" => Write(new CompleteSprintRequest(1, Sprint(SprintState.Active), ["A-1"], 3, null)),
            "start sprint" => Write(new StartSprintRequest(
                1,
                Sprint(SprintState.Future),
                _now,
                _now.AddDays(14),
                5,
                13,
                false)),
            _ => throw new ArgumentOutOfRangeException(nameof(place), place, "Unknown place"),
        };
    }

    // The command palette with a command typed, one write running (so quit and a profile switch ask first).
    private static AppState PaletteWith(string command)
    {
        var state = Detail();
        var tab = Active(state);
        var running = new RankBlockRequest(["A-1"], "A-2", true, null, ["A-1", "A-2"]);
        var started = _reducer.Reduce(state, new WriteRequested(tab.Id, running, _now)).State;
        var profiles = ProfileCatalog.Empty.With(_work).With(new Profile("dev", "https://dev.atlassian.net", "me@dev.test"))
            .Activate("work");

        return started with { Profiles = profiles, Mode = InputMode.Command, PaletteText = command };
    }

    // Esc until the question is asked (a navigable form steps to its navigate state first).
    private static AppState Discard(AppState state)
    {
        for (var step = 0; step < 4 && state.Mode != InputMode.Confirm; step++)
        {
            state = Press(state, "Esc");
        }

        return state;
    }

    private static AppState OpenBox()
    {
        var field = FormField.Create("estimate", "Estimate", FormFieldKind.Text, false, [])
            .WithInitial(FormValue.OfText("hello"));
        var other = FormField.Create("other", "Other", FormFieldKind.Text, false, []);
        var form = FormState.Opened(new AssignFormTarget("A-1", null), 3, null) with
        {
            Fields = [field, other],
            IsLoading = false,
            Navigable = true,
        };

        return Press(FormOverlaySnapshotTests.WithForm(form), "x");
    }

    private static AppState OpenCreate()
    {
        var summary = FormField.Create("summary", "Summary", FormFieldKind.Text, false, []);
        var field = FormField.Create("estimate", "Estimate", FormFieldKind.Text, false, [])
            .WithInitial(FormValue.OfText("hello"));
        var target = new CreateFormTarget("A", "A-1", false, new CreateIssueType("10", "Task", string.Empty, 0, false));
        var form = FormState.Opened(target, 3, null) with
        {
            Fields = [summary, field],
            IsLoading = false,
            Navigable = true,
            Focus = 1,
        };

        return Press(CreatePaneOverlaySnapshotTests.Open(form), "x");
    }

    private static AppState DeleteIssue()
    {
        var started = DeleteReducer.Start(Detail());
        var pending = started.State.Session.PendingDelete!;

        return DeleteReducer.Counted(started.State, new SubtasksCounted(pending.RequestId, 2, false, _now)).State;
    }

    private static AppState Write(WriteRequest request)
    {
        var state = Detail();
        var tab = Active(state);

        return _reducer.Reduce(state, new WriteRequested(tab.Id, request, _now)).State;
    }

    private static TransitionChoice Choice()
    {
        return new TransitionChoice(
            "31",
            "Start progress",
            new IssueStatus("3", "In Progress", "indeterminate"),
            false,
            true,
            []);
    }

    private static Sprint Sprint(SprintState state)
    {
        return new Sprint(8, "Sprint 2", state, null, null, null, null, 1);
    }

    private static AppState Press(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key, _now)).State);
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static AppState OnLink(AppState state, string linkId)
    {
        for (var step = 0; step < 200 && Target(state)?.LinkId != linkId; step++)
        {
            state = Press(state, "j");
        }

        Assert.Equal(linkId, Target(state)?.LinkId);

        return state;
    }

    private static DetailTarget? Target(AppState state)
    {
        var detail = Active(state).Detail[^1];

        return detail.Document!.Targets[detail.Cursor];
    }

    private static AppState OnComment(AppState state)
    {
        var tab = Active(state);
        var detail = tab.Detail[0];
        var line = detail.Document!.Targets.ToList()
            .FindIndex(static target => target is { Kind: DetailTargetKind.Comment });

        Assert.True(line >= 0, "The comment has a line in the document.");

        var moved = tab with { Detail = [detail with { Cursor = line }] };

        return state with { Session = state.Session with { Tabs = [moved] } };
    }

    // Issue A-1 open with two links and a comment, as a user sees it after the first load.
    private static AppState Detail()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me", "acc-me") },
        };
        var sized = _reducer.Reduce(start, new ViewportChanged(30, 100)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition)).State;
        var tab = Active(opened);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");
        var page = new IssuePage([row], null, null);
        var listed = _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now));
        var entered = _reducer.Reduce(listed.State, new KeyPressed("Enter", _now)).State;
        var shown = Active(entered);
        var issue = new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            _story,
            "High",
            null,
            null,
            [],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            null,
            null,
            [],
            [
                new IssueLink("1", "Blocks", IssueLinkDirection.Outward, "blocks", Stub("A-30")),
                new IssueLink("2", "Blocks", IssueLinkDirection.Inward, "is blocked by", Stub("A-9")),
            ],
            []);
        var withIssue = _reducer.Reduce(
            entered,
            new IssueDetailLoaded(shown.Id, shown.Detail[0].IssueRequest, issue, _now));
        var comment = new IssueComment(
            "1",
            new IssueAssignee("acc-other", "Olga Other"),
            AdfMarkdownParser.Parse("Please remove this"),
            _now.AddHours(-2),
            _now.AddHours(-2));
        var comments = new OffsetPage<IssueComment>([comment], 0, DetailPaging.CommentPageSize, 1, true);

        return _reducer.Reduce(
            withIssue.State,
            new IssueCommentsLoaded(shown.Id, shown.Detail[0].CommentsRequest, comments, _now)).State;
    }

    private static IssueStub Stub(string key)
    {
        return new IssueStub(key, "Other", _todo, _story);
    }
}
