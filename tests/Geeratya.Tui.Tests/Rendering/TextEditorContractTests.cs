using Geeratya.Core.Comments;
using Geeratya.Core.Editing;
using Geeratya.Core.Flash;
using Geeratya.Core.Forms;
using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// One scenario set over every text surface (<c>docs/internal/ui-consistency.md</c>, component 7): the palette, the
/// source picker, the jump prompt, the row-jump prompt, the Help filter, a form text field, a form paragraph field,
/// the profile form and the comment editor all edit through <see cref="TextBuffer"/> and draw their caret through
/// <see cref="CaretText"/>, so the same keys give the same text and caret, and the caret cell covers the character
/// under it, in every one. A new text surface is added to <see cref="Surfaces"/>.
/// </summary>
[Trait("Os", "Independent")]
public class TextEditorContractTests
{
    private const string Words = "alpha beta  gamma";

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly AppReducer _reducer = new(_keymap);

    public static TheoryData<string> Surfaces()
    {
        return ["palette", "picker", "jump", "flash", "help", "form-text", "form-paragraph", "profile", "comment"];
    }

    /// <summary>
    /// The surfaces that draw a caret cell; the Help filter and the row-jump prompt live in a title and the status
    /// bar, which are one string each (<see cref="CaretText.Marked"/>).
    /// </summary>
    public static TheoryData<string> DrawnSurfaces()
    {
        return ["palette", "picker", "jump", "form-text", "form-paragraph", "profile", "comment"];
    }

    public static TheoryData<string> LineSurfaces()
    {
        return ["form-paragraph", "comment"];
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenTheCaretInTheMiddle_WhenTyping_ThenTheTextGoesInAtTheCaretAndTheCaretStaysAfterIt(string surface)
    {
        var state = Keys(Open(surface), "a", "b", "c", "Left", "X");

        Assert.Equal(("abXc", 3), Read(surface, state));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenSeveralWords_WhenMovingByWord_ThenPunctuationBetweenWordsIsSkipped(string surface)
    {
        var typed = Keys(Open(surface), Type(Words));

        Assert.Equal((Words, 12), Read(surface, Keys(typed, "Ctrl-Left")));
        Assert.Equal((Words, 6), Read(surface, Keys(typed, "Ctrl-Left", "Ctrl-Left")));
        Assert.Equal((Words, 10), Read(surface, Keys(typed, "Ctrl-Left", "Ctrl-Left", "Alt-f")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenSeveralWords_WhenDeletingByWord_ThenOnlyThatWordGoes(string surface)
    {
        var typed = Keys(Open(surface), Type(Words));

        Assert.Equal(("alpha beta  ", 12), Read(surface, Keys(typed, "Ctrl-w")));
        Assert.Equal(("alpha   gamma", 6), Read(surface, Keys(typed, "Ctrl-Left", "Ctrl-Left", "Alt-d")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenALine_WhenGoingToItsStartAndEnd_ThenTheCaretMovesThereAndDeleteToStartKeepsTheRest(string surface)
    {
        var typed = Keys(Open(surface), Type(Words));

        Assert.Equal((Words, 0), Read(surface, Keys(typed, "Home")));
        Assert.Equal((Words, Words.Length), Read(surface, Keys(typed, "Home", "End")));
        Assert.Equal(("gamma", 0), Read(surface, Keys(typed, "Ctrl-Left", "Ctrl-u")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenWideCharacters_WhenMovingAndDeleting_ThenEachCharacterIsOneStepAndNeverSplit(string surface)
    {
        var typed = Keys(Open(surface), "a", "日", "本", "b");

        Assert.Equal(("a日本b", 3), Read(surface, Keys(typed, "Left")));
        Assert.Equal(("a日b", 2), Read(surface, Keys(typed, "Left", "Backspace")));
        Assert.Equal(("a日b", 2), Read(surface, Keys(typed, "Left", "Left", "Delete")));
    }

    [Theory]
    [MemberData(nameof(LineSurfaces))]
    public void GivenSeveralLines_WhenGoingToTheStartAndEnd_ThenTheCaretsLineIsMeantAndUpAndDownKeepTheColumn(
        string surface)
    {
        var typed = Keys(Open(surface), "o", "n", "e", "Enter", "t", "w", "o", "Enter", "x");

        Assert.Equal(("one\ntwo\nx", 8), Read(surface, Keys(typed, "Home")));
        Assert.Equal(("one\ntwo\nx", 4), Read(surface, Keys(typed, "Up", "Home")));
        Assert.Equal(("one\ntwo\nx", 7), Read(surface, Keys(typed, "Up", "End")));
        Assert.Equal(("one\ntwo\nx", 6), Read(surface, Keys(typed, "Up", "Up", "Down", "Right")));
        Assert.Equal(("one\ntwo\n", 8), Read(surface, Keys(typed, "Backspace")));
    }

    [Theory]
    [MemberData(nameof(DrawnSurfaces))]
    public void GivenTheCaretInsideTheText_WhenDrawn_ThenOneCaretCellCoversTheCharacterUnderItInTheCaretRole(
        string surface)
    {
        var state = Keys(Open(surface), Type(Words), "Left", "Left", "Left", "Left", "Left");

        var spans = Frame(state).Body.SelectMany(static line => line.Spans).ToList();

        var caret = Assert.Single(spans, static span => span.Role == TextRole.EditCaret);
        Assert.Equal("g", caret.Text);
    }

    [Theory]
    [MemberData(nameof(DrawnSurfaces))]
    public void GivenTheCaretAtTheEnd_WhenDrawn_ThenTheCaretCellIsASpaceAfterTheText(string surface)
    {
        var state = Keys(Open(surface), Type(Words));

        var spans = Frame(state).Body.SelectMany(static line => line.Spans).ToList();

        Assert.Equal(" ", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Theory]
    [InlineData("palette")]
    [InlineData("picker")]
    [InlineData("jump")]
    [InlineData("form-paragraph")]
    [InlineData("profile")]
    [InlineData("comment")]
    public void GivenTextBeyondTheLimit_WhenPasting_ThenEverySurfaceKeepsTheSameMaximum(string surface)
    {
        var pasted = new string('x', TextBuffer.MaxLength + 50);

        var state = _reducer.Reduce(Open(surface), new TextPasted(pasted)).State;

        Assert.Equal(TextBuffer.MaxLength, Read(surface, state).Text.Length);
    }

    [Theory]
    [InlineData("palette")]
    [InlineData("picker")]
    [InlineData("jump")]
    [InlineData("form-paragraph")]
    [InlineData("profile")]
    public void GivenTextShapedLikeAMention_WhenBackspacing_ThenOnlyTheCommentEditorTreatsItAsOneCharacter(
        string surface)
    {
        var token = AdfMarkdownTokens.Mention("acc-o", "Olga");

        var pasted = _reducer.Reduce(Open(surface), new TextPasted(token)).State;
        var erased = Keys(pasted, "Backspace");

        Assert.Equal(token.Length - 1, Read(surface, erased).Text.Length);
    }

    [Fact]
    public void GivenAMention_WhenBackspacingInTheCommentEditor_ThenTheWholeMentionGoes()
    {
        var token = AdfMarkdownTokens.Mention("acc-o", "Olga");

        var pasted = _reducer.Reduce(Open("comment"), new TextPasted(token)).State;

        Assert.Equal(string.Empty, Read("comment", Keys(pasted, "Backspace")).Text);
    }

    private static string[] Type(string text)
    {
        return [.. text.Select(static character => character == ' ' ? "Space" : character.ToString())];
    }

    private static FormField TextField()
    {
        return FormField.Create("estimate", "Estimate", FormFieldKind.Text, false, []);
    }

    private static StyledFrame Frame(AppState state)
    {
        return FrameBuilder.BuildStyled(state, 100, 30, _keymap)!;
    }

    private static AppState Open(string surface)
    {
        return surface switch
        {
            "palette" => AppState.Initial with { Mode = InputMode.Command },
            "picker" => AppState.Initial with
            {
                Mode = InputMode.Insert,
                Picker = new SourcePickerState(string.Empty, 0, PickerScope.All, false, null),
            },
            "jump" => AppState.Initial with { Mode = InputMode.Insert, Jump = new JumpState(string.Empty, 0, null) },
            "flash" => AppState.Initial with { Mode = InputMode.Insert, FlashJump = new FlashJumpState(string.Empty) },
            "help" => AppState.Initial with { Mode = InputMode.Help, HelpFiltering = true },
            "form-text" => OpenForm(TextField()),
            "form-paragraph" => OpenForm(
                FormField.Create("description", "Description", FormFieldKind.Paragraph, false, [])),
            "profile" => AppState.Initial with { Mode = InputMode.Insert, Form = ProfileForm.Start },
            _ => AppState.Initial with
            {
                Mode = InputMode.Insert,
                ViewportColumns = 100,
                ViewportRows = 30,
                Editor = new CommentEditor(new TabId(1), "A-1", null, TextBuffer.EmptyRich, string.Empty),
            },
        };
    }

    private static AppState OpenForm(FormField field)
    {
        var other = FormField.Create("other", "Other", FormFieldKind.Text, false, []);
        var form = FormState.Opened(new AssignFormTarget("A-1", null), 3, null) with
        {
            Fields = [field, other],
            IsLoading = false,
            Navigable = true,
        };

        return FormOverlaySnapshotTests.WithForm(form);
    }

    private static (string Text, int Cursor) Read(string surface, AppState state)
    {
        switch (surface)
        {
            case "palette":
                return (state.PaletteText, state.PaletteText.Length - state.PaletteBack);
            case "picker":
                return (state.Picker!.Text, state.Picker.Text.Length - state.Picker.Back);
            case "jump":
                return (state.Jump!.Text, state.Jump.Text.Length - state.Jump.Back);
            case "flash":
                return (state.FlashJump!.Text, state.FlashJump.Text.Length - state.FlashJump.Back);
            case "help":
                return (state.HelpFilter, state.HelpFilter.Length - state.HelpFilterBack);
            case "profile":
                return (state.Form!.Name, state.Form.Name.Length - state.Form.Back);
            case "comment":
                return (state.Editor!.Buffer.Text, state.Editor.Buffer.Cursor);
            default:
                var field = state.Session.Tabs[0].Form!.Fields[0];

                return (field.Value.Text, field.Value.Text.Length - field.Back);
        }
    }

    private AppState Keys(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key, _now)).State);
    }

    private AppState Keys(AppState state, string[] first, params string[] more)
    {
        return Keys(Keys(state, first), more);
    }
}
