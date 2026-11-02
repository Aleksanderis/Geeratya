using Geeratya.Core.Comments;
using Geeratya.Core.Editing;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Tests.Comments;

[Trait("Os", "Independent")]
public class EditorSuggestionsTests
{
    private static readonly TabId _tab = new(1);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Suggestion _olga = new("acc-olga", "Olga Other", null, false);
    private static readonly Suggestion _issue = new("GCM-12", "Fix the thing", "GCM-12", false);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenTheEditor_WhenTypingAtAndAName_ThenPeopleSuggestionsOpenForTheIssueAndFollowTheTyping()
    {
        var typed = Press(Editing(), "h", "i", "Space", "@");

        var popup = Assert.IsType<CommentEditor>(typed.State.Editor).Suggest!;
        var search = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(typed.Effects));

        Assert.Equal(SuggestionKind.IssueUsers, popup.Source.Kind);
        Assert.Equal("A-1", search.Source.IssueKey);
        Assert.Equal(string.Empty, search.Query);

        var narrowed = Press(typed.State, "o", "l");

        Assert.Equal("ol", narrowed.State.Editor!.Suggest!.Query);
        Assert.Equal("ol", Assert.IsType<SearchSuggestionsEffect>(narrowed.Effects[^1]).Query);
    }

    [Fact]
    public void GivenAMailAddressOrAHeadingLikeHash_WhenTyping_ThenNoPopupOpens()
    {
        var mail = Press(Editing(), "a", "@", "b");
        var spaced = Press(Editing(), "#", "Space");
        var hashNumber = Press(Editing(), "#", "1");

        Assert.Null(mail.State.Editor!.Suggest);
        Assert.Null(spaced.State.Editor!.Suggest);
        Assert.Null(hashNumber.State.Editor!.Suggest);
    }

    [Fact]
    public void GivenAHashAndAProjectKey_WhenTyping_ThenIssueSuggestionsOpenAndTheHashIsNotInTheQuery()
    {
        var typed = Press(Editing(), "#", "G", "C", "M", "-", "1");

        var popup = typed.State.Editor!.Suggest!;

        Assert.Equal(SuggestionKind.Issues, popup.Source.Kind);
        Assert.Equal("GCM-1", popup.Query);
    }

    [Fact]
    public void GivenAnOpenPopup_WhenTheLeadingTextIsDeleted_ThenThePopupClosesAndTheLookupIsCancelled()
    {
        var open = Press(Editing(), "@", "o");

        var closed = Press(open.State, "Backspace", "Backspace");

        Assert.Null(closed.State.Editor!.Suggest);
        Assert.Contains(closed.Effects, effect => effect is CancelSuggestionsEffect);
    }

    [Fact]
    public void GivenALoadedPersonRow_WhenPressingTabOrEnter_ThenTheTriggerBecomesAMentionTokenAndThePopupCloses()
    {
        foreach (var key in new[] { "Tab", "Enter" })
        {
            var open = Loaded(Press(Editing(), "h", "i", "Space", "@", "o", "l"), _olga);

            var accepted = Press(open.State, key);

            var editor = accepted.State.Editor!;

            Assert.Equal("hi @[Olga Other](mention:acc-olga)", editor.Buffer.Text);
            Assert.Equal(editor.Buffer.Text.Length, editor.Buffer.Cursor);
            Assert.Null(editor.Suggest);
            Assert.Contains(accepted.Effects, effect => effect is CancelSuggestionsEffect);
        }
    }

    [Fact]
    public void GivenALoadedIssueRow_WhenAccepting_ThenTheHashAndKeyBecomeALinkToTheBrowsePage()
    {
        var open = Loaded(Press(Editing(), "s", "e", "e", "Space", "#", "G", "C", "M"), _issue);

        var accepted = Press(open.State, "Tab");

        Assert.Equal("see [GCM-12](https://work.atlassian.net/browse/GCM-12)", accepted.State.Editor!.Buffer.Text);
    }

    [Fact]
    public void GivenAnAcceptedMentionAndIssue_WhenParsingTheText_ThenTheBodyHasAMentionNodeAndALinkMark()
    {
        var mention = Loaded(Press(Editing(), "@", "o"), _olga);
        var text = Press(Press(mention.State, "Tab").State, "Space", "#", "G").State;
        var withIssue = Loaded(new ReduceResult(text, []), _issue);

        var final = Press(withIssue.State, "Tab").State.Editor!.Buffer.Text;
        var paragraph = Assert.IsType<AdfParagraph>(Assert.Single(AdfMarkdownParser.Parse(final).Content));

        Assert.Contains(paragraph.Children, node => node is AdfMention { Id: "acc-olga" });
        Assert.Contains(
            paragraph.Children,
            node => node is AdfText { Text: "GCM-12", Marks: [AdfLinkMark { Href: "https://work.atlassian.net/browse/GCM-12" }] });
    }

    [Fact]
    public void GivenAnOpenPopup_WhenPressingEsc_ThenOnlyThePopupClosesAndTypingDoesNotReopenIt()
    {
        var open = Loaded(Press(Editing(), "@", "o"), _olga);

        var closed = Press(open.State, "Esc");

        Assert.NotNull(closed.State.Editor);
        Assert.Equal(InputMode.Insert, closed.State.Mode);
        Assert.Null(closed.State.Editor!.Suggest);

        var typedOn = Press(closed.State, "l");

        Assert.Null(typedOn.State.Editor!.Suggest);
        Assert.Equal("@ol", typedOn.State.Editor.Buffer.Text);
    }

    [Fact]
    public void GivenAPopupWithRows_WhenPressingDownAndUp_ThenTheHighlightMovesAndNoCursorMoves()
    {
        var rows = new[] { _olga, new Suggestion("acc-2", "Oleg", null, false) };
        var open = Loaded(Press(Editing(), "@", "o"), rows);

        var down = Press(open.State, "Down");
        var up = Press(down.State, "Up");

        Assert.Equal(1, down.State.Editor!.Suggest!.Cursor);
        Assert.Equal(0, up.State.Editor!.Suggest!.Cursor);
        Assert.Equal(2, down.State.Editor.Buffer.Cursor);
    }

    [Fact]
    public void GivenAPopupWithoutRows_WhenPressingEnter_ThenItIsANewLine()
    {
        var open = Press(Editing(), "@", "o");

        var entered = Press(open.State, "Enter");

        Assert.Equal("@o\n", entered.State.Editor!.Buffer.Text);
    }

    [Fact]
    public void GivenAStaleReply_WhenItArrives_ThenThePopupKeepsItsRows()
    {
        var open = Press(Editing(), "@", "o");
        var stale = open.State.Editor!.Suggest!.RequestId - 1;

        var result = _reducer.Reduce(open.State, new SuggestionsLoaded(_tab, stale, [_olga]));

        Assert.Empty(result.State.Editor!.Suggest!.Items);
    }

    [Fact]
    public void GivenAFailedOrSlowLookup_WhenTheCommandArrives_ThenThePopupShowsIt()
    {
        var open = Press(Editing(), "@", "o");
        var id = open.State.Editor!.Suggest!.RequestId;

        var slow = _reducer.Reduce(open.State, new SuggestionsSlow(_tab, id));
        var failed = _reducer.Reduce(slow.State, new SuggestionsFailed(_tab, id, "boom"));

        Assert.Equal(SuggestionStatus.Loading, slow.State.Editor!.Suggest!.Status);
        Assert.Equal("boom", failed.State.Editor!.Suggest!.Error);
    }

    [Fact]
    public void GivenAnOpenPopup_WhenSaving_ThenThePopupClosesFirst()
    {
        var open = Press(Editing(), "h", "i", "Space", "@");
        var saving = open.State with { Editor = open.State.Editor! with { OperationId = 7 } };

        var next = Press(saving, "Left");

        Assert.Null(next.State.Editor!.Suggest);
        Assert.Contains(next.Effects, effect => effect is CancelSuggestionsEffect);
    }

    [Fact]
    public void GivenAMentionToken_WhenUsingBackspaceDeleteAndArrows_ThenItActsAsOneCharacter()
    {
        var token = AdfMarkdownTokens.Mention("acc-olga", "Olga Other");
        var buffer = new TextBuffer($"a{token}b", 1 + token.Length);

        Assert.Equal("ab", buffer.Backspace().Text);
        Assert.Equal(1, buffer.Left().Cursor);
        Assert.Equal("ab", (buffer with { Cursor = 1 }).Delete().Text);
        Assert.Equal(1 + token.Length, (buffer with { Cursor = 1 }).Right().Cursor);
        Assert.Equal("ab", (buffer with { Cursor = 5 }).Backspace().Text);
        Assert.Equal("ab", (buffer with { Cursor = 5 }).Delete().Text);
    }

    [Fact]
    public void GivenAnIssueLinkToken_WhenBackspacing_ThenTheWholeReferenceGoes()
    {
        var token = AdfMarkdownTokens.IssueLink("https://work.atlassian.net/", "GCM-12");
        var buffer = TextBuffer.Of($"see {token}");

        Assert.Equal("see ", buffer.Backspace().Text);
    }

    private ReduceResult Loaded(ReduceResult typed, params Suggestion[] rows)
    {
        var id = typed.State.Editor!.Suggest!.RequestId;

        return _reducer.Reduce(typed.State, new SuggestionsLoaded(_tab, id, rows));
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            var next = _reducer.Reduce(result.State, new KeyPressed(key));

            result = new ReduceResult(next.State, [.. result.Effects, .. next.Effects]);
        }

        return result;
    }

    private static AppState Editing()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var editor = new CommentEditor(_tab, "A-1", null, TextBuffer.Empty, string.Empty);

        return AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active),
            Mode = InputMode.Insert,
            Editor = editor,
        };
    }
}
