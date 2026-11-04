using Geeratya.Core.Flash;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Flash;

[Trait("Os", "Independent")]
public class FlashJumpTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAListTab_WhenPressingSpaceJ_ThenThePromptOpensInInsertMode()
    {
        var result = Press(WithList(40), "Space", "j");

        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal(string.Empty, result.State.FlashJump!.Text);
    }

    [Fact]
    public void GivenNoTab_WhenPressingSpaceJ_ThenNothingOpensAndTheMessageSaysWhy()
    {
        var result = Press(AppState.Initial, "Space", "j");

        Assert.Null(result.State.FlashJump);
        Assert.Contains("list layout", result.State.Message);
    }

    [Fact]
    public void GivenAColumnsLayout_WhenPressingSpaceJ_ThenNothingOpens()
    {
        var state = WithList(5, ViewLayout.Columns);

        var result = Press(state, "Space", "j");

        Assert.Null(result.State.FlashJump);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenTextInSeveralVisibleRows_WhenTypingIt_ThenEachRowGetsADistinctLetterNearestFirst()
    {
        var state = Press(WithList(40), "Space", "j", "f", "i", "x").State;

        var labels = FlashJumpReducer.LabelsOf(state, state.FlashJump!);

        Assert.Equal(Visible(state).Count(row => row % 2 == 0), labels.Count);
        Assert.Equal(0, labels[0].Row);
        Assert.Equal(labels.Count, labels.Select(label => label.Letter).Distinct().Count());
        Assert.All(labels, label => Assert.True(label.HasLetter));
    }

    [Fact]
    public void GivenALabelLetter_WhenTypingIt_ThenTheSelectionMovesToThatRowAndThePromptCloses()
    {
        var state = Press(WithList(40), "Space", "j", "f", "i", "x").State;
        var target = FlashJumpReducer.LabelsOf(state, state.FlashJump!)[2];

        var result = Press(state, target.Letter.ToString());

        Assert.Null(result.State.FlashJump);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Equal(target.Row, Active(result.State).Selected);
        Assert.Equal($"A-{target.Row + 1}", Active(result.State).SelectedKey);
    }

    [Fact]
    public void GivenALetterThatContinuesAMatch_WhenTypingIt_ThenItExtendsTheTextInsteadOfJumping()
    {
        var state = Press(WithList(40), "Space", "j", "f", "i", "x").State;

        // The rows read "Fix login ...", so a space and an "l" continue the match and no label may use them.
        var labels = FlashJumpReducer.LabelsOf(state, state.FlashJump!);
        var next = Press(state, "Space", "l").State;

        Assert.DoesNotContain(labels, label => label.Letter is ' ' or 'l');
        Assert.Equal("fix l", next.FlashJump!.Text);
    }

    [Fact]
    public void GivenMatchesOnBothSides_WhenPressingEnter_ThenTheNearestRowIsSelected()
    {
        var state = Press(WithList(40), "j", "j", "j", "j", "j").State;

        var moved = Press(state, "Space", "j", "f", "i", "x", "Enter").State;

        Assert.Equal(4, Active(moved).Selected);
        Assert.Equal(InputMode.Normal, moved.Mode);
        Assert.Null(moved.FlashJump);
    }

    [Fact]
    public void GivenNoMatch_WhenPressingEnter_ThenThePromptStaysAndTheMessageSaysSo()
    {
        var state = Press(WithList(40), "Space", "j", "z", "q", "z").State;

        var result = Press(state, "Enter");

        Assert.NotNull(result.State.FlashJump);
        Assert.Contains("No visible row", result.State.Message);
    }

    [Fact]
    public void GivenAMatchBelowTheViewport_WhenTyping_ThenItGetsNoLabel()
    {
        var state = Press(WithList(60), "Space", "j", "A", "-", "5", "5").State;

        Assert.Empty(FlashJumpReducer.LabelsOf(state, state.FlashJump!));
    }

    [Fact]
    public void GivenAnOpenPrompt_WhenPressingBackspaceAndEsc_ThenTheTextIsEditedAndTheSelectionStays()
    {
        var state = Press(WithList(40), "Space", "j", "f", "i").State;

        var edited = Press(state, "Backspace").State;
        var cancelled = Press(edited, "Esc").State;

        Assert.Equal("f", edited.FlashJump!.Text);
        Assert.Null(cancelled.FlashJump);
        Assert.Equal(InputMode.Normal, cancelled.Mode);
        Assert.Equal(0, Active(cancelled).Selected);
    }

    [Fact]
    public void GivenAMatchPastTheSummaryColumn_WhenTyping_ThenTheRowIsNotLabelled()
    {
        var state = WithList(3, summary: _ => $"{new string('x', 150)} tail");

        var typed = Press(state, "Space", "j", "t", "a", "i", "l").State;

        Assert.Empty(FlashJumpReducer.LabelsOf(typed, typed.FlashJump!));
    }

    private static IEnumerable<int> Visible(AppState state)
    {
        var tab = Active(state);

        return Enumerable.Range(tab.Scroll, Math.Min(tab.VisibleRows, tab.Rows.Count - tab.Scroll));
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

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private AppState WithList(int count, ViewLayout layout = ViewLayout.List, Func<int, string>? summary = null)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_work) };
        var sized = _reducer.Reduce(start, new ViewportChanged(15, 100)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", layout, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var describe = summary ?? Describe;
        var rows = Enumerable.Range(0, count).Select(index => Row(index, describe(index))).ToList();
        var page = new IssuePage(rows, null, null);

        return _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    private static string Describe(int index)
    {
        return index % 2 == 0 ? $"Fix login bug {index}" : $"Add export {index}";
    }

    private static IssueSummary Row(int index, string summary)
    {
        var key = $"A-{index + 1}";

        return new IssueSummary(key, key, summary, _todo, _story, null, null, [], null, null, "A");
    }
}
