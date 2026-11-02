using System.Globalization;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Core.Writes.Comments;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Comments;

[Trait("Os", "Independent")]
public class CommentEditingTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueAssignee _me = new("acc-me", "Me Myself");
    private static readonly IssueAssignee _other = new("acc-other", "Olga Other");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAnOpenIssue_WhenPressingTheAddKeys_ThenAnEmptyEditorOpensInInsertMode()
    {
        var state = Press(OpenIssue(), "Space", "m", "a").State;

        Assert.Equal(InputMode.Insert, state.Mode);
        var editor = Assert.IsType<CommentEditor>(state.Editor);
        Assert.Equal("A-1", editor.Key);
        Assert.False(editor.IsEdit);
        Assert.Equal(string.Empty, editor.Buffer.Text);
        Assert.False(editor.IsChanged);
    }

    [Fact]
    public void GivenTheEditor_WhenPressingWordAndPageKeys_ThenTheCursorMovesByWordAndByRows()
    {
        var state = Press(OpenIssue(), "Space", "m", "a").State;
        state = Press(state, "o", "n", "e", "Space", "t", "w", "o", "Enter", "x").State;

        var wordLeft = Press(Press(state, "Ctrl-Left").State, "Ctrl-Left").State;
        var wordRight = Press(wordLeft, "Ctrl-Right").State;
        var pageUp = Press(state, "PageUp").State;
        var pageDown = Press(pageUp, "PageDown").State;

        Assert.Equal(4, Assert.IsType<CommentEditor>(wordLeft.Editor).Buffer.Cursor);
        Assert.Equal(7, Assert.IsType<CommentEditor>(wordRight.Editor).Buffer.Cursor);
        Assert.Equal(1, Assert.IsType<CommentEditor>(pageUp.Editor).Buffer.Cursor);
        Assert.Equal(9, Assert.IsType<CommentEditor>(pageDown.Editor).Buffer.Cursor);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("e")]
    [InlineData("d")]
    public void GivenAListWithNoIssueOpen_WhenPressingACommentKey_ThenTheStatusLineSaysSoAndNothingOpens(string verb)
    {
        var result = Press(ListOnly(), "Space", "m", verb);

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(result.State.Editor);
        Assert.Equal("Open an issue first.", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenTheEditor_WhenTypingEnterBackspaceAndPasting_ThenTheTextFollowsAndPasteKeepsAllLines()
    {
        var state = Press(Press(OpenIssue(), "Space", "m", "a").State, "h", "i", "Enter", "x", "Backspace", "!").State;

        state = _reducer.Reduce(state, new TextPasted(string.Concat("one", (char)13, (char)10, "two", (char)9, "end"))).State;

        Assert.Equal("hi\n!one\ntwo    end", state.Editor!.Buffer.Text);
        Assert.Equal(state.Editor.Buffer.Text.Length, state.Editor.Buffer.Cursor);
    }

    [Fact]
    public void GivenTheEditor_WhenMovingWithTheArrowKeys_ThenTypingInsertsAtTheCursor()
    {
        var state = Press(OpenIssue(), "Space", "m", "a").State;

        state = Press(state, "a", "b", "Enter", "c", "d").State;
        state = Press(state, "Up", "Home", "X", "End", "Y", "Left", "Left", "Delete").State;

        Assert.Equal("XaY\ncd", state.Editor!.Buffer.Text);
    }

    [Fact]
    public void GivenTypedText_WhenPressingCtrlS_ThenOneAddWriteStartsAndTheEditorShowsSaving()
    {
        var typed = Typed("Looks good");

        var result = Press(typed, "Ctrl-s");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));
        var request = Assert.IsType<AddCommentRequest>(effect.Request);
        Assert.Equal("A-1", request.Key);
        Assert.Equal("Looks good", AdfMarkdownWriter.ToText(request.Body));
        Assert.Equal("acc-me", request.AuthorAccountId);
        Assert.Equal(2, request.AfterId);
        Assert.True(result.State.Editor!.IsSaving);
        Assert.Equal(effect.OperationId, result.State.Editor.OperationId);
        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal("Looks good", result.State.Editor.Buffer.Text);
        var operation = Assert.Single(result.State.Session.Operations);
        Assert.Equal(OperationKind.AddComment, operation.Kind);
    }

    [Fact]
    public void GivenASaveInFlight_WhenTypingOrSavingAgain_ThenNothingChangesAndNoSecondWriteStarts()
    {
        var saving = Press(Typed("Hello"), "Ctrl-s").State;

        var typed = Press(saving, "x", "Enter", "Backspace");
        var again = Press(saving, "Ctrl-s");
        var cancelled = Press(saving, "Esc");

        Assert.Equal("Hello", typed.State.Editor!.Buffer.Text);
        Assert.Empty(again.Effects);
        Assert.Single(again.State.Session.Operations);
        Assert.Equal(InputMode.Insert, cancelled.State.Mode);
        Assert.True(cancelled.State.Editor!.IsSaving);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n \n")]
    public void GivenAnEmptyOrBlankText_WhenPressingCtrlS_ThenNothingIsSentAndTheEditorSaysWhy(string text)
    {
        var state = _reducer.Reduce(Press(OpenIssue(), "Space", "m", "a").State, new TextPasted(text)).State;

        var result = Press(state, "Ctrl-s");

        Assert.Empty(result.Effects);
        Assert.Empty(result.State.Session.Operations);
        Assert.Equal("A comment cannot be empty.", result.State.Editor!.Error);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Fact]
    public void GivenASavedComment_WhenJiraAnswers_ThenTheEditorClosesTheCommentShowsAndNoIssueReloadStarts()
    {
        var saving = Press(Typed("Looks good"), "Ctrl-s");
        var id = Assert.Single(saving.Effects.OfType<ExecuteWriteEffect>()).OperationId;
        var stored = Comment(77, _me, "Looks good");

        var done = _reducer.Reduce(saving.State, new WriteSucceeded(id, _now, [], stored));

        Assert.Null(done.State.Editor);
        Assert.Equal(InputMode.Normal, done.State.Mode);
        Assert.Equal("Added a comment to A-1", done.State.Message);
        Assert.Empty(done.State.Session.Operations);
        var detail = Active(done.State).Detail[0];
        Assert.Equal(["1", "2", "77"], detail.Comments.Select(comment => comment.Id));
        Assert.Equal(3, detail.CommentTotal);
        Assert.Contains("Comments (3)", Text(detail));
        Assert.Empty(done.Effects.OfType<LoadIssueDetailEffect>());
        Assert.Empty(done.Effects.OfType<InvalidateCacheEffect>());
        Assert.False(Active(done.State).IsStale);
        Assert.False(detail.IssueStale);
    }

    [Fact]
    public void GivenMoreCommentPagesUnloaded_WhenACommentIsAdded_ThenItIsNotShownOutOfPlaceButTheCountGrows()
    {
        var saving = Press(Typed("Late"), "Ctrl-s");
        var withMore = WithNextPage(saving.State);
        var id = withMore.Session.Operations[0].Id;

        var done = _reducer.Reduce(withMore, new WriteSucceeded(id, _now, [], Comment(90, _me, "Late")));

        var detail = Active(done.State).Detail[0];
        Assert.Equal(["1", "2"], detail.Comments.Select(comment => comment.Id));
        Assert.Equal(6, detail.CommentTotal);
    }

    [Fact]
    public void GivenTheSameIssueInAnotherTab_WhenACommentIsAdded_ThenBothTabsShowIt()
    {
        var both = OpenSecondTab(Typed("Shared"));
        var saving = Press(both, "Ctrl-s");
        var id = saving.State.Session.Operations[0].Id;

        var done = _reducer.Reduce(saving.State, new WriteSucceeded(id, _now, [], Comment(78, _me, "Shared")));

        Assert.All(done.State.Session.Tabs, tab => Assert.Equal(3, tab.Detail[0].Comments.Count));
    }

    [Fact]
    public void GivenARefusedSave_WhenJiraAnswers403_ThenTheEditorStaysWithTheTextAndTheReasonAndCanSaveAgain()
    {
        var saving = Press(Typed("Hello"), "Ctrl-s");
        var id = saving.State.Session.Operations[0].Id;
        var error = new WriteError(WriteErrorKind.Permission, 403, "No", [], ["You may not comment"]);

        var failed = _reducer.Reduce(saving.State, new WriteFailed(id, _now, error));

        var editor = Assert.IsType<CommentEditor>(failed.State.Editor);
        Assert.Equal(InputMode.Insert, failed.State.Mode);
        Assert.Equal("Hello", editor.Buffer.Text);
        Assert.False(editor.IsSaving);
        Assert.Equal("No permission to add a comment to A-1: You may not comment", editor.Error);
        Assert.Null(failed.State.Message);
        Assert.Empty(failed.State.Session.Operations);

        var typedAgain = Press(failed.State, "!").State;
        Assert.Null(typedAgain.Editor!.Error);

        var retry = Press(typedAgain, "Ctrl-s");
        Assert.IsType<AddCommentRequest>(Assert.Single(retry.Effects.OfType<ExecuteWriteEffect>()).Request);
    }

    [Fact]
    public void GivenALostReply_WhenTheCheckFindsTheComment_ThenTheEditorClosesWithoutSendingAgain()
    {
        var saving = Press(Typed("Hello"), "Ctrl-s");
        var id = saving.State.Session.Operations[0].Id;
        var unknown = _reducer.Reduce(
            saving.State,
            new WriteOutcomeUnknown(id, _now, new WriteError(WriteErrorKind.Timeout, null, "t", [], [])));

        Assert.True(unknown.State.Editor!.IsSaving);
        Assert.IsType<VerifyWriteEffect>(unknown.Effects[0]);

        var verified = _reducer.Reduce(
            unknown.State,
            new WriteVerified(id, _now, VerifyOutcome.Applied, [], Comment(79, _me, "Hello")));

        Assert.Null(verified.State.Editor);
        Assert.Contains("79", Active(verified.State).Detail[0].Comments.Select(comment => comment.Id));
        Assert.Empty(verified.Effects.OfType<ExecuteWriteEffect>());
    }

    [Fact]
    public void GivenALostReply_WhenTheCheckFindsNothing_ThenTheEditorStaysOpenWithTheTextAndSaysItWasNotAdded()
    {
        var saving = Press(Typed("Hello"), "Ctrl-s");
        var id = saving.State.Session.Operations[0].Id;
        var unknown = _reducer.Reduce(
            saving.State,
            new WriteOutcomeUnknown(id, _now, new WriteError(WriteErrorKind.Timeout, null, "t", [], [])));

        var verified = _reducer.Reduce(unknown.State, new WriteVerified(id, _now, VerifyOutcome.NotApplied, []));

        Assert.Equal("Hello", verified.State.Editor!.Buffer.Text);
        Assert.False(verified.State.Editor.IsSaving);
        Assert.Equal("A-1: the comment was not added", verified.State.Editor.Error);
    }

    [Fact]
    public void GivenALostReply_WhenTheCheckCannotRead_ThenTheEditorStaysOpenAndTellsToReloadFirst()
    {
        var saving = Press(Typed("Hello"), "Ctrl-s");
        var id = saving.State.Session.Operations[0].Id;
        var unknown = _reducer.Reduce(
            saving.State,
            new WriteOutcomeUnknown(id, _now, new WriteError(WriteErrorKind.Timeout, null, "t", [], [])));

        var verified = _reducer.Reduce(unknown.State, new WriteVerified(id, _now, VerifyOutcome.Unreachable, []));

        Assert.Contains("could not check the result", verified.State.Editor!.Error);
        Assert.Equal("Hello", verified.State.Editor.Buffer.Text);
    }

    [Fact]
    public void GivenAWriteRunningOnTheIssue_WhenSaving_ThenNothingStartsAndTheEditorSaysTheIssueIsBusy()
    {
        var typed = Typed("Hello");
        var busy = _reducer.Reduce(
            typed,
            new WriteRequested(Active(typed).Id, new AssignRequest("A-1", _me), _now)).State;

        var result = Press(busy, "Ctrl-s");

        Assert.Empty(result.Effects);
        Assert.Equal("A-1 is busy (assigning)", result.State.Editor!.Error);
        Assert.False(result.State.Editor.IsSaving);
        Assert.Single(result.State.Session.Operations);
    }

    [Fact]
    public void GivenUnchangedText_WhenPressingEsc_ThenTheEditorClosesAtOnce()
    {
        var state = Press(OpenIssue(), "Space", "m", "a").State;

        var result = Press(state, "Esc");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(result.State.Editor);
        Assert.Null(result.State.Confirm);
    }

    [Fact]
    public void GivenChangedText_WhenPressingEsc_ThenItAsksAndNoKeepsTheTextAndYesThrowsItAway()
    {
        var asked = Press(Typed("Draft"), "Esc").State;

        Assert.Equal(InputMode.Confirm, asked.Mode);
        Assert.Contains("Discard", asked.Confirm!.Message);
        Assert.Equal("Draft", asked.Editor!.Buffer.Text);

        var kept = Press(asked, "n").State;
        Assert.Equal(InputMode.Insert, kept.Mode);
        Assert.Null(kept.Confirm);
        Assert.Equal("Draft", kept.Editor!.Buffer.Text);

        var thrown = Press(asked, "y").State;
        Assert.Equal(InputMode.Normal, thrown.Mode);
        Assert.Null(thrown.Editor);
        Assert.Empty(thrown.Session.Operations);
    }

    [Fact]
    public void GivenTheDiscardQuestion_WhenPressingEscOrEnter_ThenEscKeepsTheTextAndEnterNeverConfirms()
    {
        var asked = Press(Typed("Draft"), "Esc").State;

        var enter = Press(asked, "Enter").State;
        var esc = Press(asked, "Esc").State;

        Assert.Equal(InputMode.Confirm, enter.Mode);
        Assert.Equal(InputMode.Insert, esc.Mode);
        Assert.Equal("Draft", esc.Editor!.Buffer.Text);
    }

    [Fact]
    public void GivenTheCursorOnAComment_WhenPressingTheEditKeys_ThenTheEditorOpensWithItsTextAsMarkdown()
    {
        var state = OnComment(OpenIssue(), "1", Comment(1, _me, "**bold** text"));

        var result = Press(state, "Space", "m", "e");

        var editor = Assert.IsType<CommentEditor>(result.State.Editor);
        Assert.Equal("1", editor.CommentId);
        Assert.Equal("**bold** text", editor.Buffer.Text);
        Assert.Null(editor.Warning);
        Assert.False(editor.IsChanged);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Fact]
    public void GivenNoCommentUnderTheCursor_WhenPressingTheEditOrDeleteKeys_ThenTheStatusLineSaysSo()
    {
        var state = OpenIssue();

        var edit = Press(state, "Space", "m", "e");
        var delete = Press(state, "Space", "m", "d");

        Assert.Equal("Put the cursor on a comment first.", edit.State.Message);
        Assert.Equal("Put the cursor on a comment first.", delete.State.Message);
        Assert.Null(edit.State.Editor);
        Assert.Null(delete.State.Confirm);
    }

    [Fact]
    public void GivenSomeoneElsesComment_WhenEditing_ThenTheEditorWarnsThatJiraMayRefuse()
    {
        var state = OnComment(OpenIssue(), "1", Comment(1, _other, "Theirs"));

        var editor = Press(state, "Space", "m", "e").State.Editor!;

        Assert.Contains("Not your comment", editor.Warning);
    }

    [Fact]
    public void GivenACommentMentioningPeople_WhenEditing_ThenOneCheckPerDistinctPersonIsAskedAndNewCommentsAskNothing()
    {
        var text = "@[Olga Other](mention:acc-other) and @[Olga Other](mention:acc-other) and @[Me Myself](mention:acc-me)";
        var state = OnComment(OpenIssue(), "1", Comment(1, _me, text));

        var edit = Press(state, "Space", "m", "e");
        var add = Press(OpenIssue(), "Space", "m", "a");
        var plain = Press(OnComment(OpenIssue(), "1", Comment(1, _me, "no mention")), "Space", "m", "e");

        var check = Assert.IsType<CheckMentionsEffect>(Assert.Single(edit.Effects));
        Assert.Equal(["acc-other", "acc-me"], check.People.Select(person => person.Id));
        Assert.Equal("Olga Other", check.People[0].Name);
        Assert.Equal("1", check.CommentId);
        Assert.Empty(add.Effects);
        Assert.Empty(plain.Effects);
    }

    [Fact]
    public void GivenTheMentionCheckAnswers_WhenItIsForTheOpenEditor_ThenThePersonIsFlaggedUntilTheMentionIsDeleted()
    {
        var text = "hi @[Olga Other](mention:acc-other)";
        var opened = Press(OnComment(OpenIssue(), "1", Comment(1, _me, text)), "Space", "m", "e");
        var editor = opened.State.Editor!;
        var olga = new MentionedPerson("acc-other", "Olga Other");

        var flagged = _reducer.Reduce(opened.State, new MentionsChecked(editor.Tab, editor.Key, "1", [olga]));
        var stale = _reducer.Reduce(opened.State, new MentionsChecked(editor.Tab, editor.Key, "2", [olga]));
        var deleted = Press(flagged.State, "Backspace");

        Assert.Equal([olga], flagged.State.Editor!.UnseenNow);
        Assert.Empty(stale.State.Editor!.UnseenNow);
        Assert.Empty(deleted.State.Editor!.UnseenNow);
        Assert.Equal("hi ", deleted.State.Editor.Buffer.Text);
    }

    [Fact]
    public void GivenAForeignCommentWithControlCharacters_WhenEditing_ThenTheEditorTextHasNoneOfThem()
    {
        var hostile = string.Concat("a", (char)27, "[31m", (char)9, "b", (char)13, (char)10, "c", (char)7);
        var comment = new IssueComment("1", _other, AdfMarkdownParser.Parse(hostile), _now, _now);

        var editor = Press(OnComment(OpenIssue(), "1", comment), "Space", "m", "e").State.Editor!;

        Assert.DoesNotContain(editor.Buffer.Text, character => char.IsControl(character) && character != (char)10);
        Assert.Contains("    b", editor.Buffer.Text);
        Assert.False(editor.IsChanged);
    }

    [Fact]
    public void GivenACommentWithATable_WhenEditing_ThenTheEditorWarnsThatFormattingIsReplaced()
    {
        var table = AdfParser.Parse(
            """{"version":1,"type":"doc","content":[{"type":"table","content":[{"type":"tableRow","content":[{"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"cell"}]}]}]}]}]}""");
        var comment = new IssueComment("1", _me, table, _now, _now);

        var editor = Press(OnComment(OpenIssue(), "1", comment), "Space", "m", "e").State.Editor!;

        Assert.Contains("formatting", editor.Warning);
    }

    [Fact]
    public void GivenAnUnchangedEdit_WhenPressingCtrlS_ThenNothingIsSentAndTheEditorCloses()
    {
        var state = OnComment(OpenIssue(), "1", Comment(1, _me, "Same"));
        var editing = Press(state, "Space", "m", "e").State;

        var result = Press(editing, "Ctrl-s");

        Assert.Empty(result.Effects);
        Assert.Null(result.State.Editor);
        Assert.Equal("No change to save.", result.State.Message);
    }

    [Fact]
    public void GivenAChangedEdit_WhenSavedAndAccepted_ThenTheCommentIsReplacedInPlace()
    {
        var state = OnComment(OpenIssue(), "1", Comment(1, _me, "Old"));
        var editing = Press(state, "Space", "m", "e").State;
        var saving = Press(editing, "!", "Ctrl-s");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(saving.Effects));
        var request = Assert.IsType<EditCommentRequest>(effect.Request);
        Assert.Equal("1", request.CommentId);
        Assert.Equal("Old!", AdfMarkdownWriter.ToText(request.Body));

        var done = _reducer.Reduce(saving.State, new WriteSucceeded(effect.OperationId, _now, [], Comment(1, _me, "Old!")));

        var detail = Active(done.State).Detail[0];
        Assert.Null(done.State.Editor);
        Assert.Equal(["1", "2"], detail.Comments.Select(comment => comment.Id));
        Assert.Contains("Old!", Text(detail));
        Assert.Equal("Saved the comment on A-1", done.State.Message);
    }

    [Fact]
    public void GivenSomeoneElsesComment_WhenJiraAnswers403_ThenTheEditorStaysAndNamesTheMissingPermission()
    {
        var state = OnComment(OpenIssue(), "1", Comment(1, _other, "Theirs"));
        var saving = Press(Press(state, "Space", "m", "e").State, "!", "Ctrl-s");
        var id = saving.State.Session.Operations[0].Id;
        var error = new WriteError(WriteErrorKind.Permission, 403, "Forbidden", [], ["You can only edit your own"]);

        var failed = _reducer.Reduce(saving.State, new WriteFailed(id, _now, error));

        Assert.Equal(
            "No permission to edit the comment on A-1: You can only edit your own",
            failed.State.Editor!.Error);
        Assert.Equal("Theirs!", failed.State.Editor.Buffer.Text);
    }

    [Fact]
    public void GivenTheCursorOnAComment_WhenPressingTheDeleteKeys_ThenItAsksNamingTheCommentAndThatThereIsNoUndo()
    {
        var state = OnComment(OpenIssue(), "1", Comment(1, _other, "Please remove this"));

        var asked = Press(state, "Space", "m", "d");

        Assert.Equal(InputMode.Confirm, asked.State.Mode);
        Assert.Contains("A-1", asked.State.Confirm!.Message);
        Assert.Contains("Jira cannot undo", asked.State.Confirm.Message);
        Assert.Contains("Olga Other", asked.State.Confirm.Message);
        Assert.Contains("Please remove this", asked.State.Confirm.Message);
        Assert.Empty(asked.Effects);
        Assert.Empty(asked.State.Session.Operations);
    }

    [Fact]
    public void GivenTheDeleteQuestion_WhenAnsweringNoOrEscOrEnter_ThenNothingIsDeleted()
    {
        var asked = Press(OnComment(OpenIssue(), "1", Comment(1, _me, "x")), "Space", "m", "d").State;

        var no = Press(asked, "n");
        var esc = Press(asked, "Esc");
        var enter = Press(asked, "Enter");

        Assert.Empty(no.Effects);
        Assert.Empty(esc.Effects);
        Assert.Empty(enter.Effects);
        Assert.Equal(InputMode.Confirm, enter.State.Mode);
        Assert.Equal(2, Active(no.State).Detail[0].Comments.Count);
    }

    [Fact]
    public void GivenTheDeleteQuestion_WhenAnsweringYes_ThenOneDeleteWriteStartsAndTheCommentGoesWhenJiraAccepts()
    {
        var asked = Press(OnComment(OpenIssue(), "1", Comment(1, _me, "x")), "Space", "m", "d").State;

        var yes = Press(asked, "y");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(yes.Effects));
        Assert.Equal(new DeleteCommentRequest("A-1", "1"), effect.Request);
        Assert.Equal(2, Active(yes.State).Detail[0].Comments.Count);
        Assert.Equal("Deleting the comment on A-1", yes.State.Message);

        var done = _reducer.Reduce(yes.State, new WriteSucceeded(effect.OperationId, _now, []));

        var detail = Active(done.State).Detail[0];
        Assert.Equal(["2"], detail.Comments.Select(comment => comment.Id));
        Assert.Equal(1, detail.CommentTotal);
        Assert.Equal("Deleted the comment on A-1", done.State.Message);
        Assert.Empty(done.State.Session.Operations);
    }

    [Fact]
    public void GivenADeleteJiraRefuses_WhenItAnswers403_ThenTheCommentStaysAndTheStatusLineSaysWhy()
    {
        var asked = Press(OnComment(OpenIssue(), "1", Comment(1, _other, "x")), "Space", "m", "d").State;
        var yes = Press(asked, "y");
        var id = yes.State.Session.Operations[0].Id;
        var error = new WriteError(WriteErrorKind.Permission, 403, "Forbidden", [], ["Not allowed"]);

        var failed = _reducer.Reduce(yes.State, new WriteFailed(id, _now, error));

        Assert.Equal("No permission to delete the comment on A-1: Not allowed", failed.State.Message);
        Assert.Equal(2, Active(failed.State).Detail[0].Comments.Count);
    }

    [Fact]
    public void GivenADeleteWhoseReplyIsLost_WhenTheCheckFindsItGone_ThenTheCommentIsRemoved()
    {
        var asked = Press(OnComment(OpenIssue(), "1", Comment(1, _me, "x")), "Space", "m", "d").State;
        var yes = Press(asked, "y");
        var id = yes.State.Session.Operations[0].Id;
        var unknown = _reducer.Reduce(
            yes.State,
            new WriteOutcomeUnknown(id, _now, new WriteError(WriteErrorKind.Timeout, null, "t", [], [])));

        var verified = _reducer.Reduce(unknown.State, new WriteVerified(id, _now, VerifyOutcome.Applied, []));

        Assert.Equal(["2"], Active(verified.State).Detail[0].Comments.Select(comment => comment.Id));
    }

    [Fact]
    public void GivenTheEnterKeyOnAComment_WhenPressed_ThenNothingOpensAndTheBrowserKeyOpensTheIssue()
    {
        var state = OnComment(OpenIssue(), "1", Comment(1, _me, "x"));

        var enter = Press(state, "Enter");
        var browser = Press(state, "Space", "o");

        Assert.Empty(enter.Effects);
        Assert.Equal(state.Session.Tabs, enter.State.Session.Tabs);
        var opened = Assert.IsType<OpenUrlEffect>(Assert.Single(browser.Effects));
        Assert.Equal("https://work.atlassian.net/browse/A-1", opened.Url.AbsoluteUri);
    }

    [Fact]
    public void GivenAnAddedCommentWithoutReply_WhenTheWriteSucceeds_ThenTheFirstCommentPageIsLoadedAgain()
    {
        var saving = Press(Typed("Hello"), "Ctrl-s");
        var id = saving.State.Session.Operations[0].Id;

        var done = _reducer.Reduce(saving.State, new WriteSucceeded(id, _now, []));

        var reload = Assert.Single(done.Effects.OfType<LoadIssueCommentsEffect>());
        Assert.Equal(0, reload.StartAt);
        Assert.Equal(reload.RequestId, Active(done.State).Detail[0].CommentsRequest);
        Assert.Null(done.State.Editor);
    }

    [Fact]
    public void GivenTwoComments_WhenLayingOutTheIssue_ThenEveryLineOfACommentOpensItAndTheGapBetweenOpensNothing()
    {
        var document = Active(OpenIssue()).Detail[0].Document!;
        var lines = document.Lines.Select(line => string.Concat(line.Spans.Select(span => span.Text))).ToList();
        var first = lines.FindIndex(line => line.Contains("First", StringComparison.Ordinal));
        var second = lines.FindIndex(line => line.Contains("Second", StringComparison.Ordinal));

        Assert.Equal(new DetailTarget(DetailTargetKind.Comment, "1"), document.Targets[first - 1]);
        Assert.Equal(new DetailTarget(DetailTargetKind.Comment, "1"), document.Targets[first]);
        Assert.Equal(new DetailTarget(DetailTargetKind.Comment, "2"), document.Targets[second - 1]);
        Assert.Equal(new DetailTarget(DetailTargetKind.Comment, "2"), document.Targets[second]);
        Assert.Null(document.Targets[second - 2]);
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

    private AppState Typed(string text)
    {
        var editing = Press(OpenIssue(), "Space", "m", "a").State;

        return _reducer.Reduce(editing, new TextPasted(text)).State;
    }

    private static AppState WithNextPage(AppState state)
    {
        var tab = Active(state);
        var detail = tab.Detail[0] with { NextComments = 2, CommentTotal = 5 };
        var replaced = tab with { Detail = [detail] };

        return state with { Session = state.Session with { Tabs = [replaced] } };
    }

    /// <summary>A second tab that shows the same issue; the first tab's editor text is carried over.</summary>
    private AppState OpenSecondTab(AppState typed)
    {
        var first = Active(typed);
        var second = _reducer.Reduce(
            typed with { Mode = InputMode.Normal, Editor = null },
            new ViewOpenRequested(Definition())).State;
        var tab = Active(second);
        var page = new IssuePage([Row(1)], null, null);
        var loaded = _reducer.Reduce(second, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
        var opened = _reducer.Reduce(loaded, new KeyPressed("Enter")).State;
        var shown = Active(opened);
        var withIssue = _reducer.Reduce(
            opened,
            new IssueDetailLoaded(shown.Id, shown.Detail[0].IssueRequest, Detailed("A-1"), _now)).State;
        var withComments = _reducer.Reduce(
            withIssue,
            new IssueCommentsLoaded(shown.Id, shown.Detail[0].CommentsRequest, CommentPage(), _now)).State;

        return withComments with { Mode = InputMode.Insert, Editor = typed.Editor! with { Tab = first.Id } };
    }

    /// <summary>The cursor of the open issue on a line of comment <paramref name="id"/> (replacing its content).</summary>
    private AppState OnComment(AppState state, string id, IssueComment comment)
    {
        var tab = Active(state);
        var replaced = tab.Detail[0].Comments.Select(existing => existing.Id == id ? comment : existing).ToArray();
        var changed = DetailChange(state, tab.Detail[0] with { Comments = replaced });
        var detail = Active(changed).Detail[0];
        var line = detail.Document!.Targets.ToList()
            .FindIndex(target => target is { Kind: DetailTargetKind.Comment } && target.Value == id);

        Assert.True(line >= 0, "The comment has a line in the document.");

        return DetailChange(changed, detail with { Cursor = line });
    }

    private AppState DetailChange(AppState state, DetailState detail)
    {
        var tab = Active(state);
        var relaid = _reducer.Reduce(
            state with { Session = state.Session with { Tabs = [tab with { Detail = [detail] }] } },
            new ViewportChanged(30, 81));

        // A new width makes the reducer lay the document out again from the changed comments.
        return _reducer.Reduce(relaid.State, new ViewportChanged(30, 80)).State;
    }

    private AppState OpenIssue()
    {
        var opened = _reducer.Reduce(ListOnly(), new KeyPressed("Enter")).State;
        var tab = Active(opened);
        var withIssue = _reducer.Reduce(
            opened,
            new IssueDetailLoaded(tab.Id, tab.Detail[0].IssueRequest, Detailed("A-1"), _now)).State;

        return _reducer.Reduce(
            withIssue,
            new IssueCommentsLoaded(tab.Id, tab.Detail[0].CommentsRequest, CommentPage(), _now)).State;
    }

    private AppState ListOnly()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me Myself", "acc-me") },
        };
        var sized = _reducer.Reduce(start, new ViewportChanged(30, 80)).State;
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(Definition())).State;
        var tab = Active(opened);
        var page = new IssuePage([Row(1), Row(2)], null, null);

        return _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static string Text(DetailState detail)
    {
        return string.Join('\n', detail.Document!.Lines.Select(line => string.Concat(line.Spans.Select(span => span.Text))));
    }

    private static ViewDefinition Definition()
    {
        return new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
    }

    private static OffsetPage<IssueComment> CommentPage()
    {
        return new OffsetPage<IssueComment>(
            [Comment(1, _me, "First"), Comment(2, _other, "Second")],
            0,
            DetailPaging.CommentPageSize,
            2,
            true);
    }

    private static IssueComment Comment(int number, IssueAssignee author, string markdown)
    {
        var body = AdfMarkdownParser.Parse(markdown);

        return new IssueComment(number.ToString(CultureInfo.InvariantCulture), author, body, _now.AddHours(-2), _now.AddHours(-2));
    }

    private static IssueDetail Detailed(string key)
    {
        return new IssueDetail(
            "1",
            key,
            "Issue 1",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            "High",
            null,
            new IssueAssignee("r", "Rita Reporter"),
            [],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            null,
            null,
            [],
            [],
            []);
    }

    private static IssueSummary Row(int number)
    {
        return new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
            $"A-{number}",
            $"Issue {number}",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }
}
