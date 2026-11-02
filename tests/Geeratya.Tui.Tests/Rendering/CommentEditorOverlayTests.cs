using Geeratya.Core.Comments;
using Geeratya.Core.Editing;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class CommentEditorOverlayTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenANewComment_WhenRendering_ThenTheBoxShowsTitleTextPlaceholderAndKeys()
    {
        var frame = Frame(Editor(string.Empty), 80, 24);
        var text = string.Join('\n', frame.PlainLines());

        Assert.Contains("Comment on A-1", text);
        Assert.Contains("Type the comment here", text);
        Assert.Contains("Ctrl-s send", text);
        Assert.Contains("Enter new line", text);
        Assert.Contains("Esc cancel", text);
        Assert.Contains("INSERT Commenting on A-1", frame.StatusLine);
        Assert.EndsWith("Ctrl-s save  Esc cancel", frame.StatusLine);
    }

    [Fact]
    public void GivenAnEdit_WhenRendering_ThenTheTitleSaysEditAndTheFooterSaysSave()
    {
        var editor = Editor("old") with { CommentId = "5", Original = "old" };

        var text = string.Join('\n', Frame(editor, 80, 24).PlainLines());

        Assert.Contains("Edit comment on A-1", text);
        Assert.Contains("Ctrl-s save", text);
    }

    [Fact]
    public void GivenAMentionAndALink_WhenRendering_ThenTheBoxShowsShortTokensAndNoAddress()
    {
        var mention = AdfMarkdownTokens.Mention("acc-o", "Olga Other");
        var link = AdfMarkdownTokens.IssueLink("https://x.atlassian.net", "A-12");
        var text = $"hi {mention} see {link}";

        var shown = string.Join('\n', Frame(Editor(text), 80, 24).PlainLines());

        Assert.Contains("hi @Olga Other see A-12", shown);
        Assert.DoesNotContain("mention:", shown);
        Assert.DoesNotContain("browse", shown);
    }

    [Fact]
    public void GivenTheCursorOnAMention_WhenRendering_ThenTheWholeShortTokenIsSelected()
    {
        var text = $"hi {AdfMarkdownTokens.Mention("acc-o", "Olga")}!";
        var editor = Editor(text) with { Buffer = new TextBuffer(text, 3) };

        var body = Frame(editor, 80, 24).Body;
        var line = Assert.Single(body, row => row.PlainText.Contains("hi @Olga", StringComparison.Ordinal));

        Assert.Contains(line.Spans, span => span.Role == TextRole.Selected && span.Text == "@Olga");
    }

    [Theory]
    [InlineData(3, "@日本語", "hi ", "!")]
    [InlineData(0, "h", "", "i @日本語!")]
    [InlineData(4, "!", "hi @日本語", "")]
    public void GivenAWideNamedMention_WhenRenderingWithTheCursorAround_ThenTheSelectedCellIsRight(
        int cursorOffset,
        string selected,
        string before,
        string after)
    {
        var token = AdfMarkdownTokens.Mention("acc-o", "日本語");
        var text = $"hi {token}!";
        var cursor = cursorOffset == 4 ? text.Length - 1 : cursorOffset;
        var editor = Editor(text) with { Buffer = new TextBuffer(text, cursor) };

        var body = Frame(editor, 80, 24).Body;
        var line = Assert.Single(body, row => row.PlainText.Contains("@日本語", StringComparison.Ordinal));
        var spans = line.Spans.Where(span => span.Text.Length > 0).ToList();

        Assert.Contains(spans, span => span.Role == TextRole.Selected && span.Text == selected);
        Assert.Contains(spans, span => span.Role == TextRole.Body && span.Text == before || before.Length == 0);
        Assert.Contains(spans, span => span.Role == TextRole.Body && span.Text == after || after.Length == 0);
    }

    [Fact]
    public void GivenAMentionOfSomeoneWhoMayNotSeeTheIssue_WhenRendering_ThenAWarningNamesThem()
    {
        var editor = Editor("hi @[Olga Other](mention:acc-o)") with
        {
            CommentId = "5",
            Original = "hi @[Olga Other](mention:acc-o)",
            Unseen = [new MentionedPerson("acc-o", "Olga Other")],
        };

        var frame = Frame(editor, 80, 24);

        var line = Assert.Single(frame.Body, row => row.PlainText.Contains("May not see A-1", StringComparison.Ordinal));
        Assert.Contains("Olga Other", line.PlainText);
        Assert.Contains(line.Spans, span => span.Role == TextRole.Stale && span.Text.Contains("Olga Other", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenText_WhenRendering_ThenTheCursorIsASelectedCellAtItsPosition()
    {
        var editor = Editor("hello") with { Buffer = new TextBuffer("hello", 2) };

        var line = Assert.Single(Frame(editor, 80, 24).Body, row => row.Spans.Any(span => span.Role == TextRole.Selected));

        var selected = Assert.Single(line.Spans, span => span.Role == TextRole.Selected);
        Assert.Equal("l", selected.Text);
        Assert.Contains("he", line.PlainText);
    }

    [Fact]
    public void GivenTheCursorAtTheEnd_WhenRendering_ThenTheCursorIsASelectedSpace()
    {
        var line = Assert.Single(
            Frame(Editor("hello"), 80, 24).Body,
            row => row.Spans.Any(span => span.Role == TextRole.Selected));

        Assert.Equal(" ", Assert.Single(line.Spans, span => span.Role == TextRole.Selected).Text);
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(120, 36)]
    [InlineData(40, 10)]
    [InlineData(24, 8)]
    [InlineData(20, 6)]
    [InlineData(12, 3)]
    [InlineData(5, 2)]
    [InlineData(1, 1)]
    public void GivenAnySize_WhenRenderingLongWideText_ThenEveryLineFitsAndNothingThrows(int width, int height)
    {
        var text = string.Concat(Enumerable.Repeat("日本語の長い文章 and a 😀 long line\n", 30));
        var editor = Editor(text) with { Warning = "Careful with the formatting", Error = "Jira said no" };

        var lines = Frame(editor, width, height).PlainLines();

        Assert.Equal(height, lines.Count);
        Assert.All(lines, line => Assert.True(TextWidth.Of(line) <= width, line));
    }

    [Fact]
    public void GivenMoreRowsThanTheBoxHolds_WhenTheCursorIsAtTheEnd_ThenTheLastRowsShowAndTheFirstDoNot()
    {
        var text = string.Join('\n', Enumerable.Range(1, 60).Select(number => $"line {number:D2}"));

        var plain = string.Join('\n', Frame(Editor(text), 80, 24).PlainLines());

        Assert.Contains("line 60", plain);
        Assert.DoesNotContain("line 01", plain);
    }

    [Fact]
    public void GivenTheCursorMovedToTheTop_WhenRendering_ThenTheFirstRowsShow()
    {
        var text = string.Join('\n', Enumerable.Range(1, 60).Select(number => $"line {number:D2}"));
        var editor = Editor(text) with { Buffer = new TextBuffer(text, 0) };

        var plain = string.Join('\n', Frame(editor, 80, 24).PlainLines());

        Assert.Contains("line 01", plain);
        Assert.DoesNotContain("line 60", plain);
    }

    [Fact]
    public void GivenAWideCharacterAtTheWrapPoint_WhenRendering_ThenItIsNeverSplit()
    {
        var text = string.Concat(Enumerable.Repeat("日", 80));

        var lines = Frame(Editor(text), 40, 24).PlainLines();

        Assert.Equal(80, lines.Sum(line => line.Count(character => character == '日')));
    }

    [Fact]
    public void GivenSavingAnErrorAndAWarning_WhenRendering_ThenEachHasItsLine()
    {
        var editor = Editor("x") with { OperationId = 9, Error = "No permission to edit", Warning = "Not your comment" };

        var frame = Frame(editor, 80, 24);
        var plain = string.Join('\n', frame.PlainLines());

        Assert.Contains("Saving…", plain);
        Assert.Contains("No permission to edit", plain);
        Assert.Contains("Not your comment", plain);
        Assert.Contains("saving…", frame.StatusLine);
        Assert.Contains(frame.Body, row => row.Spans.Any(span => span.Role == TextRole.Error));
        Assert.Contains(frame.Body, row => row.Spans.Any(span => span.Role == TextRole.Loading));
    }

    [Fact]
    public void GivenAPopupWithRows_WhenRendering_ThenTheRowsShowUnderTheTextWithTheChosenRowMarkedAndTheFooterChanges()
    {
        var rows = new[]
        {
            new Suggestion("acc-1", "Olga Other", null, false),
            new Suggestion("acc-2", "Oleg Two", null, true),
        };
        var source = new SuggestionSource(SuggestionKind.IssueUsers, "A-1", []);
        var popup = new SuggestionState(0, source, "ol", [], rows, 1, SuggestionStatus.Loaded, 3, null);

        var frame = Frame(Editor("hi @ol") with { Suggest = popup }, 80, 24);
        var plain = string.Join('\n', frame.PlainLines());

        Assert.Contains("  Olga Other", plain);
        Assert.Contains("> Oleg Two (inactive)", plain);
        Assert.Contains("Down/Up choose   Tab/Enter accept   Esc close list", plain);
        Assert.Contains(frame.Body, row => row.Spans.Any(span => span.Role == TextRole.Selected && span.Text.StartsWith("> Oleg")));
    }

    [Theory]
    [InlineData(SuggestionStatus.Loading, "searching…")]
    [InlineData(SuggestionStatus.Failed, "lookup failed: boom")]
    [InlineData(SuggestionStatus.Loaded, "No match")]
    public void GivenAPopupWithoutRows_WhenRendering_ThenTheLookupStateShows(SuggestionStatus status, string expected)
    {
        var source = new SuggestionSource(SuggestionKind.Issues, "A-1", []);
        var popup = new SuggestionState(0, source, "GCM", [], [], 0, status, 3, "boom");

        var plain = string.Join('\n', Frame(Editor("#GCM") with { Suggest = popup }, 80, 24).PlainLines());

        Assert.Contains(expected, plain);
        Assert.Contains("Ctrl-s send", plain);
    }

    [Fact]
    public void GivenTheEditorHints_WhenCheckingTheDefaultKeymap_ThenEachBindsItsActionInInsertMode()
    {
        foreach (var hint in StatusBar.EditorHints)
        {
            Assert.NotNull(_keymap.FindKeys(hint.Mode, hint.ActionId));
        }
    }

    [Fact]
    public void GivenTheDiscardQuestion_WhenRendering_ThenTheEditorStaysUnderTheConfirmLine()
    {
        var state = StateWith(Editor("draft")) with
        {
            Mode = InputMode.Confirm,
            Confirm = new ConfirmRequest("Discard the unsent comment text? It is not saved anywhere", "comment.discard"),
        };

        var frame = FrameBuilder.BuildStyled(state, 80, 24, [], _keymap)!;

        Assert.Contains("CONFIRM Discard the unsent comment text?", frame.StatusLine);
        Assert.Contains("draft", string.Join('\n', frame.PlainLines()));
    }

    private static StyledFrame Frame(CommentEditor editor, int width, int height)
    {
        return FrameBuilder.BuildStyled(StateWith(editor), width, height, [], _keymap)!;
    }

    private static AppState StateWith(CommentEditor editor)
    {
        return AppState.Initial with { Mode = InputMode.Insert, Editor = editor };
    }

    private static CommentEditor Editor(string text)
    {
        return new CommentEditor(new TabId(1), "A-1", null, TextBuffer.Of(text), string.Empty);
    }
}
