using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Links;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Links.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Links;

/// <summary>Creating and removing links through the keymap and the reducer, as the shell drives them.</summary>
[Trait("Os", "Independent")]
public class LinkFlowTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenARowSelected_WhenTheLinkKeyIsPressed_ThenTheFormOpensAndLoadsTheLinkTypes()
    {
        var result = Press(InList(), "Space", "i", "l");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal(new LinkFormTarget("A-1"), effect.Target);
        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal("Link A-1", Active(result.State).Form!.Target.Title);
        Assert.Equal("https://work.atlassian.net/browse/A-1", Active(result.State).Form!.BrowserUrl);
    }

    [Fact]
    public void GivenAnOpenIssue_WhenTheLinkKeyIsPressed_ThenTheShownIssueIsTheTarget()
    {
        var result = Press(InDetail(), "Space", "i", "l");

        Assert.Equal("A-1", Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects)).Target.IssueKey);
    }

    [Fact]
    public void GivenNoIssue_WhenTheLinkKeyIsPressed_ThenOnlyTheStatusSaysWhy()
    {
        var state = AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work) };

        var result = Press(state, "Space", "i", "l");

        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Contains("No issue selected", result.State.Message);
    }

    [Fact]
    public void GivenTheFilledForm_WhenSubmitted_ThenOneLinkWriteStartsWithTheChosenSide()
    {
        var state = LoadedForm();

        state = Press(state, "Down").State;
        state = Press(state, "Enter").State;
        state = Type(state, "a-30");
        var submitted = Press(state, "Ctrl-s");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects));
        var request = Assert.IsType<LinkIssuesRequest>(effect.Request);
        Assert.Equal("is blocked by", request.DirectionText);
        Assert.Equal("A-30", request.OutwardKey);
        Assert.Equal("A-1", request.InwardKey);
        Assert.Equal("Linking A-1 is blocked by A-30", submitted.State.Message);
    }

    [Fact]
    public void GivenTheFormWithoutAnIssue_WhenSubmitted_ThenNothingIsSentAndTheFieldIsRequired()
    {
        var state = Press(LoadedForm(), "Enter").State;

        var submitted = Press(state, "Ctrl-s");

        Assert.Empty(submitted.Effects);
        Assert.Equal("Required", Active(submitted.State).Form!.Fields[1].Error);
    }

    [Fact]
    public void GivenARefusedKey_WhenTheWriteFails_ThenTheFormStaysOpenWithTheMessageAtTheIssueField()
    {
        var state = Type(Press(LoadedForm(), "Enter").State, "A-1");
        var sent = Press(state, "Ctrl-s");
        var operation = sent.State.Session.Operations.Single();
        var error = new WriteError(
            WriteErrorKind.Validation,
            null,
            "An issue cannot be linked to itself.",
            [new WriteFieldError(LinkFormFields.IssueFieldId, "An issue cannot be linked to itself.")],
            []);

        var failed = _reducer.Reduce(sent.State, new WriteFailed(operation.Id, _now, error));

        var form = Active(failed.State).Form!;
        Assert.Equal("An issue cannot be linked to itself.", form.Fields[1].Error);
        Assert.Null(form.OperationId);
        Assert.Equal(InputMode.Insert, failed.State.Mode);
    }

    [Fact]
    public void GivenALinkWriteThatSucceeded_WhenSettled_ThenBothIssuesAreReloadedAndNoTabGoesStale()
    {
        var state = InDetail();
        var sent = Press(Press(state, "Space", "i", "l").State, "Esc").State;
        var request = new LinkIssuesRequest("A-1", "A-30", "10000", IssueLinkDirection.Outward, "blocks");
        var started = _reducer.Reduce(sent, new WriteRequested(Active(sent).Id, request, _now));
        var operation = started.State.Session.Operations.Single();

        var done = _reducer.Reduce(started.State, new WriteSucceeded(operation.Id, _now, []));

        Assert.Empty(done.State.Session.Operations);
        Assert.False(Active(done.State).IsStale);
        Assert.Equal("Linked A-1 blocks A-30", done.State.Message);
        Assert.Equal(["A-1", "A-30"], done.Effects.OfType<InvalidateCacheEffect>().Single().Keys);
        Assert.Equal("A-1", done.Effects.OfType<LoadIssueDetailEffect>().Single().Key);
    }

    [Fact]
    public void GivenAnIssueBeingWritten_WhenLinkingToIt_ThenTheLinkIsRefusedAsBusy()
    {
        var state = InDetail();
        var assign = new AssignRequest("A-30", null);
        var busy = _reducer.Reduce(state, new WriteRequested(Active(state).Id, assign, _now)).State;
        var link = new LinkIssuesRequest("A-1", "A-30", "10000", IssueLinkDirection.Outward, "blocks");

        var refused = _reducer.Reduce(busy, new WriteRequested(Active(busy).Id, link, _now));

        Assert.Empty(refused.Effects);
        Assert.Equal("A-30 is busy (assigning)", refused.State.Message);
    }

    [Fact]
    public void GivenTheCursorOnALinkedIssue_WhenTheUnlinkKeyIsPressed_ThenConfirmNamesBothIssuesAndSaysNoUndo()
    {
        var state = OnLink(InDetail(), "2");

        var result = Press(state, "Space", "i", "u");

        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Equal("Remove the link A-1 is blocked by A-9? Jira cannot undo this.", result.State.Confirm!.Message);
    }

    [Fact]
    public void GivenTheUnlinkConfirm_WhenAnswering_ThenOnlyYStartsTheDeleteOfThatLinkId()
    {
        var asked = Press(OnLink(InDetail(), "1"), "Space", "i", "u").State;

        var enter = Press(asked, "Enter");
        var no = Press(asked, "n");
        var yes = Press(asked, "y");

        Assert.Equal(InputMode.Confirm, enter.State.Mode);
        Assert.Empty(enter.Effects);
        Assert.Empty(no.Effects);
        Assert.Equal(InputMode.Normal, no.State.Mode);
        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(yes.Effects));
        var request = Assert.IsType<UnlinkIssuesRequest>(effect.Request);
        Assert.Equal(("1", "A-30", "A-1"), (request.LinkId, request.OtherKey, request.Key));
        Assert.Equal("Removing the link A-1 blocks A-30", yes.State.Message);
    }

    [Fact]
    public void GivenTheIssueBecameBusyBeforeTheAnswer_WhenConfirming_ThenTheRemovalIsRefused()
    {
        var state = OnLink(InDetail(), "1");
        var asked = Press(state, "Space", "i", "u").State;
        var assign = new AssignRequest("A-30", null);
        var running = new Operation(99, assign, Active(asked).Id, OperationPhase.Sending, [], _now);
        var busy = asked with { Session = asked.Session with { Operations = [running] } };

        var yes = Press(busy, "y");

        Assert.Empty(yes.Effects);
        Assert.Equal("A-30 is busy (assigning)", yes.State.Message);
    }

    [Fact]
    public void GivenTheCursorNotOnALink_WhenTheUnlinkKeyIsPressed_ThenNothingIsAskedAndTheStatusSaysWhy()
    {
        var result = Press(InDetail(), "Space", "i", "u");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(result.State.Confirm);
        Assert.Equal("The cursor is not on a linked issue.", result.State.Message);
    }

    [Fact]
    public void GivenNoIssueOpen_WhenTheUnlinkKeyIsPressed_ThenNothingIsAskedAndTheStatusSaysWhy()
    {
        var result = Press(InList(), "Space", "i", "u");

        Assert.Null(result.State.Confirm);
        Assert.Contains("Open an issue", result.State.Message);
    }

    [Fact]
    public void GivenALinkRemoved_WhenSettled_ThenTheDetailReloadsAndTheStatusSaysSo()
    {
        var asked = Press(OnLink(InDetail(), "1"), "Space", "i", "u").State;
        var yes = Press(asked, "y");
        var operation = yes.State.Session.Operations.Single();

        var done = _reducer.Reduce(yes.State, new WriteSucceeded(operation.Id, _now, []));

        Assert.Equal("Removed the link A-1 blocks A-30", done.State.Message);
        Assert.Equal("A-1", done.Effects.OfType<LoadIssueDetailEffect>().Single().Key);
        Assert.False(Active(done.State).IsStale);
    }

    [Fact]
    public void GivenNoPermission_WhenRemovingTheLink_ThenTheStatusNamesWhatWasNotDone()
    {
        var asked = Press(OnLink(InDetail(), "1"), "Space", "i", "u").State;
        var yes = Press(asked, "y");
        var operation = yes.State.Session.Operations.Single();
        var error = new WriteError(WriteErrorKind.Permission, 403, "No link permission.", [], ["No link permission."]);

        var failed = _reducer.Reduce(yes.State, new WriteFailed(operation.Id, _now, error));

        Assert.Equal("No permission to remove the link A-1 blocks A-30: No link permission.", failed.State.Message);
        Assert.Empty(failed.State.Session.Operations);
    }

    private AppState LoadedForm()
    {
        var opened = Press(InList(), "Space", "i", "l").State;
        var tab = Active(opened);
        var types = new[] { new IssueLinkType("10000", "Blocks", "is blocked by", "blocks") };

        return _reducer.Reduce(opened, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, LinkFormFields.From(types)))
            .State;
    }

    private AppState OnLink(AppState state, string linkId)
    {
        var steps = 0;

        while (Target(state)?.LinkId != linkId && steps++ < 200)
        {
            state = Press(state, "j").State;
        }

        Assert.Equal(linkId, Target(state)?.LinkId);

        return state;
    }

    private static DetailTarget? Target(AppState state)
    {
        var detail = Active(state).Detail[^1];

        return detail.Document!.Targets[detail.Cursor];
    }

    private AppState Type(AppState state, string text)
    {
        return text.Aggregate(state, (current, character) => Press(current, character.ToString()).State);
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key, _now));
        }

        return result;
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private AppState InList()
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

        return _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    /// <summary>Issue A-1 open with two links: it blocks A-30 (id 1) and is blocked by A-9 (id 2).</summary>
    private AppState InDetail()
    {
        var state = Press(InList(), "Enter").State;
        var tab = Active(state);
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

        return _reducer.Reduce(state, new IssueDetailLoaded(tab.Id, tab.Detail[0].IssueRequest, issue, _now)).State;
    }

    private static IssueStub Stub(string key)
    {
        return new IssueStub(key, "Other", _todo, _story);
    }
}
