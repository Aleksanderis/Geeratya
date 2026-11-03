using Geeratya.Core.History;
using Geeratya.Core.Detail;
using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Jump;

[Trait("Os", "Independent")]
public class JumpReducerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenNoProfile_WhenPressingSpaceG_ThenNothingOpensAndTheMessageSaysToAddOne()
    {
        var result = Press(AppState.Initial, "Space", "g");

        Assert.Null(result.State.Jump);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Contains(":profile add", result.State.Message);
    }

    [Fact]
    public void GivenAListTab_WhenPressingSpaceG_ThenThePromptOpensInInsertModeWithTheTabKeysOffered()
    {
        var result = Press(WithList(), "Space", "g");

        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal(string.Empty, result.State.Jump!.Text);
        Assert.Equal(["A-1", "A-2"], Rows(result.State).Select(row => row.Key));
        Assert.IsType<LoadHistoryEffect>(Assert.Single(result.Effects));
    }

    [Fact]
    public void GivenAPromptInInsertMode_WhenPressingSpaceG_ThenItIsTypedAsText()
    {
        var state = Press(WithList(), "Space", "g").State;

        var typed = Press(state, "Space", "g").State;

        Assert.Equal(" g", typed.Jump!.Text);
    }

    [Fact]
    public void GivenALowerCaseKeyTyped_WhenPressingEnter_ThenTheIssueOpensOnTopOfTheTabAndNothingIsWritten()
    {
        var state = Press(WithList(), "Space", "g").State;

        var result = Press(state, "a", "-", "2", "Enter");

        var tab = Active(result.State);
        Assert.Equal("A-2", Assert.Single(tab.Detail).Key);
        Assert.Equal("A-2", Assert.Single(result.Effects.OfType<LoadIssueDetailEffect>()).Key);
        Assert.Equal(4, result.Effects.Count);
        Assert.Single(result.Effects.OfType<RecordHistoryEffect>());
        Assert.Null(result.State.Jump);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenABareNumberOnAListOfProjectA_WhenPressingEnter_ThenTheKeyIsCompletedWithTheProject()
    {
        var state = Press(WithList(), "Space", "g").State;

        var result = Press(state, "2", "Enter");

        Assert.Equal("A-2", Active(result.State).Detail[0].Key);
    }

    [Fact]
    public void GivenABareNumberNotInTheTab_WhenPressingEnter_ThenTheTypedKeyOpensNotARowThatContainsIt()
    {
        var state = Press(WithList(), "Space", "g").State;

        var result = Press(state, "1", "7", "Enter");

        Assert.Equal("A-17", Active(result.State).Detail[0].Key);
    }

    [Fact]
    public void GivenAnIssueOpen_WhenJumpingByNumber_ThenTheProjectOfTheShownIssueIsUsed()
    {
        var opened = Press(WithList(), "Enter").State;
        var other = Press(Press(opened, "Space", "g").State, "B", "-", "5", "Enter").State;

        var result = Press(other, "Space", "g", "9", "Enter");

        Assert.Equal(["A-1", "B-5", "B-9"], Active(result.State).Detail.Select(detail => detail.Key));
    }

    [Fact]
    public void GivenGarbage_WhenPressingEnter_ThenThePromptStaysOpenAndSaysItIsNotAKey()
    {
        var state = Press(WithList(), "Space", "g").State;

        var result = Press(state, "x", "y", "z", "Enter");

        Assert.NotNull(result.State.Jump);
        Assert.Contains("not an issue key", result.State.Jump!.Notice);
        Assert.Empty(Active(result.State).Detail);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenPartialTextThatCompletesARow_WhenPressingEnter_ThenTheHighlightedCompletionOpens()
    {
        var state = Press(WithList(), "Space", "g").State;

        var result = Press(state, "a", "Enter");

        Assert.Equal("A-1", Active(result.State).Detail[0].Key);
        Assert.Null(result.State.Jump);
    }

    [Fact]
    public void GivenANumberAndNoProjectKnown_WhenPressingEnter_ThenTheNoticeAsksForTheFullKey()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_work) };

        var result = Press(state, "Space", "g", "1", "2", "Enter");

        Assert.Contains("full key", result.State.Jump!.Notice);
        Assert.Empty(result.State.Session.Tabs);
    }

    [Fact]
    public void GivenNothingTyped_WhenPressingEnterWithNoRows_ThenTheNoticeSaysToTypeAKey()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_work) };

        var result = Press(state, "Space", "g", "Enter");

        Assert.Contains("Type an issue key", result.State.Jump!.Notice);
    }

    [Fact]
    public void GivenNoTab_WhenJumping_ThenAKeyTabOpensAndTheIssueIsLoadedOnTopOfIt()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_work) };

        var result = Press(state, "Space", "g", "a", "-", "1", "Enter");

        var tab = Active(result.State);
        Assert.Equal("key = A-1", tab.Definition.SourceValue);
        Assert.Equal("A-1", Assert.Single(tab.Detail).Key);
        Assert.Single(result.Effects.OfType<LoadViewPageEffect>());
        Assert.Single(result.Effects.OfType<LoadIssueDetailEffect>());
    }

    [Fact]
    public void GivenAFullStack_WhenJumping_ThenNothingOpensAndTheMessageSaysSo()
    {
        var state = Press(WithList(), "Enter").State;

        for (var index = 1; index < DetailPaging.MaxStack; index++)
        {
            state = Press(state, "Space", "g", $"A-{index + 10}", "Enter").State;
        }

        var result = Press(state, "Space", "g", "A", "-", "9", "9", "Enter");

        Assert.DoesNotContain(Active(result.State).Detail, detail => detail.Key == "A-99");
        Assert.Contains("Too many issues", result.State.Message);
    }

    [Fact]
    public void GivenAnIssueLoaded_WhenItArrives_ThenItsKeyIsRememberedNewestFirstWithoutDuplicates()
    {
        var state = Press(WithList(), "Enter").State;
        var tab = Active(state);
        var detail = tab.Detail[0];

        var loaded = _reducer.Reduce(state, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Detailed("A-1"), _now));
        var again = Remember(loaded.State, "A-5", "A-1");

        Assert.Equal(["A-1"], loaded.State.Session.RecentKeys);
        Assert.Equal(["A-1", "A-5"], again.Session.RecentKeys);
    }

    [Fact]
    public void GivenMoreThanTheLimit_WhenRemembering_ThenTheOldestAreDropped()
    {
        var state = WithList();

        state = Remember(state, [.. Enumerable.Range(1, JumpRows.MaxRecent + 5).Select(number => $"A-{number}")]);

        Assert.Equal(JumpRows.MaxRecent, state.Session.RecentKeys.Count);
        Assert.Equal($"A-{JumpRows.MaxRecent + 5}", state.Session.RecentKeys[0]);
    }

    [Fact]
    public void GivenRecentKeys_WhenTyping_ThenTheyCompleteAfterTheTypedKeyAndFilterByText()
    {
        var state = Remember(WithList(), "B-7", "B-70", "C-1");
        var typed = Press(state, "Space", "g", "b", "-", "7").State;

        var rows = Rows(typed);

        Assert.Equal(["B-7", "B-70"], rows.Select(row => row.Key));
        Assert.Equal(JumpOrigin.Typed, rows[0].Origin);
        Assert.Equal(JumpOrigin.Recent, rows[1].Origin);
    }

    [Fact]
    public void GivenProfileHistoryNotLoaded_WhenOpeningThePrompt_ThenTheHistoryIsRequested()
    {
        var result = Press(WithList(), "Space", "g");

        Assert.Equal(_work, Assert.Single(result.Effects.OfType<LoadHistoryEffect>()).Profile);
    }

    [Fact]
    public void GivenProfileHistory_WhenThePromptIsEmpty_ThenRecentKeysComeFirstNewestFirstAndEnterOpensOne()
    {
        var state = WithHistory(WithList(), "H-2", "H-1");

        var opened = Press(state, "Space", "g");

        Assert.Empty(opened.Effects);
        Assert.Equal(["H-2", "H-1", "A-1", "A-2"], Rows(opened.State).Select(row => row.Key));
        Assert.Equal(JumpOrigin.Recent, Rows(opened.State)[0].Origin);
        Assert.Equal("H-1", Active(Press(opened.State, "Down", "Enter").State).Detail[0].Key);
    }

    [Fact]
    public void GivenARecentKeyTheTabKnows_WhenThePromptIsEmpty_ThenTheRowKeepsTheTabTitle()
    {
        var state = WithHistory(WithList(), "A-2");

        var rows = Rows(Press(state, "Space", "g").State);

        Assert.Equal(["A-2", "A-1"], rows.Select(row => row.Key));
        Assert.NotEmpty(rows[0].Title);
        Assert.Equal(JumpOrigin.Recent, rows[0].Origin);
    }

    [Fact]
    public void GivenProfileHistoryAndSessionKeys_WhenTyping_ThenTheyFilterWithoutRepeats()
    {
        var state = WithHistory(Remember(WithList(), "H-2"), "H-2", "H-10", "B-1");

        var typed = Press(state, "Space", "g", "h", "-").State;

        Assert.Equal(["H-2", "H-10"], Rows(typed).Where(row => row.Origin == JumpOrigin.Recent).Select(row => row.Key));
    }

    [Fact]
    public void GivenAChosenCompletion_WhenPressingEnter_ThenThatKeyOpensNotTheTypedOne()
    {
        var state = Remember(WithList(), "B-7", "B-70");
        var typed = Press(state, "Space", "g", "b", "-", "7").State;

        var result = Press(typed, "Down", "Enter");

        Assert.Equal("B-70", Active(result.State).Detail[0].Key);
    }

    [Fact]
    public void GivenTheHighlightAtTheEnds_WhenMoving_ThenItStopsThere()
    {
        var state = Press(WithList(), "Space", "g").State;

        var up = Press(state, "Up").State;
        var down = Press(state, "Down", "Down", "Down", "Down").State;

        Assert.Equal(0, up.Jump!.Selected);
        Assert.Equal(1, down.Jump!.Selected);
    }

    [Fact]
    public void GivenTypedText_WhenPressingBackspace_ThenTheLastTextElementGoesAndEmptyTextStaysOpen()
    {
        var state = Press(WithList(), "Space", "g", "a", "é").State;

        var erased = Press(state, "Backspace").State;
        var empty = Press(erased, "Backspace", "Backspace").State;

        Assert.Equal("a", erased.Jump!.Text);
        Assert.Equal(string.Empty, empty.Jump!.Text);
        Assert.Equal(InputMode.Insert, empty.Mode);
    }

    [Fact]
    public void GivenAnOpenPrompt_WhenPressingEscOrCtrlC_ThenItClosesWithoutOpeningAnything()
    {
        var state = Press(WithList(), "Space", "g", "A", "-", "2").State;

        var esc = Press(state, "Esc");
        var control = Press(state, "Ctrl-c");

        Assert.Null(esc.State.Jump);
        Assert.Null(control.State.Jump);
        Assert.Empty(Active(esc.State).Detail);
        Assert.Empty(esc.Effects);
    }

    [Fact]
    public void GivenAPaste_WhenThePromptIsOpen_ThenTheFirstLineIsTyped()
    {
        var state = Press(WithList(), "Space", "g").State;

        var pasted = _reducer.Reduce(state, new TextPasted("gcm-3\nignored")).State;

        Assert.Equal("gcm-3", pasted.Jump!.Text);
    }

    [Fact]
    public void GivenTheJumpKey_WhenFindingTheDefaultKeymap_ThenItIsBoundAndTitled()
    {
        var keymap = DefaultKeymap.Load();

        Assert.Equal("Space g", keymap.FindKeys(InputMode.Normal, ActionIds.IssueJump));
        Assert.NotNull(ActionCatalog.FindTitle(ActionIds.IssueJump));
    }

    [Fact]
    public void GivenTheTabTarget_WhenJumping_ThenANewTabOpensWithTheIssueOnTopAndTheOldTabIsUntouched()
    {
        var state = WithList();
        var before = Active(state);
        state = state with { Settings = state.Settings with { JumpTarget = JumpTarget.Tab } };

        var typed = Press(state, "Space", "g").State;
        var result = Press(typed, "A", "-", "2", "Enter");

        Assert.Equal(2, result.State.Session.Tabs.Count);
        var created = Active(result.State);
        Assert.NotEqual(before.Id, created.Id);
        Assert.Equal("key = A-2", created.Definition.SourceValue);
        Assert.Equal("A-2", Assert.Single(created.Detail).Key);
        Assert.Empty(result.State.Session.Tabs.Single(tab => tab.Id == before.Id).Detail);
    }

    [Fact]
    public void GivenTheCurrentTarget_WhenJumping_ThenNoTabIsAdded()
    {
        var state = Press(WithList(), "Space", "g").State;

        var result = Press(state, "A", "-", "2", "Enter");

        Assert.Single(result.State.Session.Tabs);
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

    private static IReadOnlyList<JumpRow> Rows(AppState state)
    {
        return JumpRows.For(state, state.Jump!);
    }

    private static AppState Remember(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, JumpReducer.Remember);
    }

    private static AppState WithHistory(AppState state, params string[] newestFirst)
    {
        var history = new SiteHistory([], newestFirst);
        var sources = state.Session.Sources with { History = history };

        return state with { Session = state.Session with { Sources = sources } };
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private AppState WithList()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_work) };
        var sized = _reducer.Reduce(start, new ViewportChanged(12, 80)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var page = new IssuePage([Row("A-1"), Row("A-2")], null, null);

        return _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    private static IssueSummary Row(string key)
    {
        return new IssueSummary(key, key, $"Summary of {key}", _todo, _story, null, null, [], null, null, "A");
    }

    private static IssueDetail Detailed(string key)
    {
        return new IssueDetail(
            "1",
            key,
            "Issue 1",
            _todo,
            _story,
            "High",
            null,
            null,
            [],
            _now.AddDays(-3),
            _now,
            "A",
            null,
            null,
            [],
            [],
            []);
    }
}
