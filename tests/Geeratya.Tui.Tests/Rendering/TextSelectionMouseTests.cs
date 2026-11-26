using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// Drag, double-click and click in the three selecting text areas (the comment editor, the create form's Summary and
/// Description, a field form's text field), through the drawn regions, the real resolver and the real reducer: the
/// regions must start where the text is drawn, or a click would select the wrong character.
/// </summary>
public class TextSelectionMouseTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly DateTimeOffset _at = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenCommentText_WhenDoubleClickingAWord_ThenThatWordIsSelected()
    {
        var state = CommentEditorOverlayTests.StateWith(CommentEditorOverlayTests.Editor("hello big world"));
        var frame = Frame(state);

        var result = Gesture(state, frame, "big", 1, MouseGesture.DoubleClick);

        Assert.Equal("big", result.Editor!.Buffer.SelectedText);
    }

    [Fact]
    public void GivenCommentText_WhenClicking_ThenTheCaretGoesUnderThePointerAndTheSelectionClears()
    {
        var editor = CommentEditorOverlayTests.Editor("hello world");
        var state = CommentEditorOverlayTests.StateWith(editor with { Buffer = editor.Buffer.SelectAll() });

        var result = Gesture(state, Frame(state), "world", 2, MouseGesture.Click);

        Assert.Equal(8, result.Editor!.Buffer.Cursor);
        Assert.False(result.Editor.Buffer.HasSelection);
    }

    [Fact]
    public void GivenCommentText_WhenDraggingAcrossRows_ThenTheTextBetweenIsSelectedAndKeysReplaceIt()
    {
        var state = CommentEditorOverlayTests.StateWith(CommentEditorOverlayTests.Editor("first line\nsecond line"));
        var frame = Frame(state);
        var start = Cell(frame, "first", 2);
        var end = Cell(frame, "second", 3);

        var dragged = Drag(state, frame, start, end, false);

        Assert.Equal("rst line\nsec", dragged.Editor!.Buffer.SelectedText);

        var typed = new AppReducer(_keymap).Reduce(dragged, new KeyPressed("x", _at)).State;

        Assert.Equal("fixond line", typed.Editor!.Buffer.Text);
    }

    [Fact]
    public void GivenALiveDrag_WhenTheMovesRepeatAndTheButtonIsReleased_ThenTheAnchorStays()
    {
        var state = CommentEditorOverlayTests.StateWith(CommentEditorOverlayTests.Editor("one two three"));
        var frame = Frame(state);
        var start = Cell(frame, "two", 0);
        var first = Drag(state, frame, start, Cell(frame, "three", 0), false);
        var later = Drag(first, Frame(first), start, Cell(frame, "three", 3), true);

        Assert.Equal("two thr", later.Editor!.Buffer.SelectedText);
    }

    [Fact]
    public void GivenCreateFormDescription_WhenDoubleClickingAndDragging_ThenItSelectsInTheDescriptionField()
    {
        var form = CreateForm("a title", "alpha beta\ngamma delta", 1);
        var state = CreatePaneOverlaySnapshotTests.Open(form);
        var frame = Frame(state);

        var word = Gesture(state, frame, "delta", 1, MouseGesture.DoubleClick);

        Assert.Equal("delta", FormEditor.SelectedText(FormOf(word).Fields[1]));

        var dragged = Drag(state, frame, Cell(frame, "beta", 1), Cell(frame, "gamma", 2), false);

        Assert.Equal("eta\nga", FormEditor.SelectedText(FormOf(dragged).Fields[1]));
    }

    [Fact]
    public void GivenCreateFormSummary_WhenClickingAndDoubleClicking_ThenTheSummaryCaretAndWordFollow()
    {
        var form = CreateForm("fix the login", string.Empty, 0);
        var state = CreatePaneOverlaySnapshotTests.Open(form);
        var frame = Frame(state);

        var placed = Gesture(state, frame, "login", 3, MouseGesture.Click);
        var word = Gesture(state, frame, "the", 1, MouseGesture.DoubleClick);

        Assert.Equal(2, FormOf(placed).Fields[0].Back);
        Assert.Equal("the", FormEditor.SelectedText(FormOf(word).Fields[0]));
    }

    [Fact]
    public void GivenCreateFormInNavigateState_WhenClickingTheText_ThenNoTextRegionIsDrawn()
    {
        var form = CreateForm("fix the login", string.Empty, 0) with { Navigating = true };
        var frame = Frame(CreatePaneOverlaySnapshotTests.Open(form));

        Assert.DoesNotContain(frame.Regions, region => region.Target == MouseTarget.EditorText);
    }

    [Fact]
    public void GivenALongSummaryCutAroundItsCaret_WhenClickingAVisibleWord_ThenTheIndexIsInTheFullText()
    {
        var text = string.Join(' ', Enumerable.Repeat("wordy", 40)) + " tail-end";
        var form = CreateForm(text, string.Empty, 0);
        var state = CreatePaneOverlaySnapshotTests.Open(form);
        var frame = Frame(state);

        var word = Gesture(state, frame, "tail", 0, MouseGesture.DoubleClick);

        Assert.Equal("tail", FormEditor.SelectedText(FormOf(word).Fields[0]));
    }

    [Fact]
    public void GivenAFieldFormTextField_WhenDoubleClickingAWord_ThenItIsSelected()
    {
        var form = FormOverlaySnapshotTests.SampleForm();
        var summary = form.Fields[1] with { Value = FormValue.OfText("pay the invoice"), Back = 0, Error = null };
        var state = FormOverlaySnapshotTests.WithForm(
            form with { Fields = [form.Fields[0], summary, .. form.Fields.Skip(2)], Focus = 1 });
        var frame = Frame(state);

        var word = Gesture(state, frame, "invoice", 2, MouseGesture.DoubleClick);

        Assert.Equal("invoice", FormEditor.SelectedText(FormOf(word).Fields[1]));
    }

    [Fact]
    public void GivenTheEditorTextRegions_WhenReadingThem_ThenEachStartsWhereItsTextIsDrawn()
    {
        var state = CommentEditorOverlayTests.StateWith(CommentEditorOverlayTests.Editor("alpha\nbeta"));
        var frame = Frame(state);
        var lines = frame.PlainLines();
        var rows = frame.Regions.Where(region => region.Target == MouseTarget.EditorText).ToList();

        Assert.Equal([0, 1], rows.Select(row => row.Line));
        Assert.StartsWith("alpha", lines[rows[0].Top][rows[0].Left..]);
        Assert.StartsWith("beta", lines[rows[1].Top][rows[1].Left..]);
    }

    private static FormState CreateForm(string summary, string description, int focus)
    {
        var form = CreatePaneOverlaySnapshotTests.Form();
        var fields = form.Fields.ToArray();

        fields[0] = fields[0] with { Value = FormValue.OfText(summary), Back = 0 };
        fields[1] = fields[1] with { Value = FormValue.OfText(description), Back = 0 };

        return form with { Fields = fields, Focus = focus, Navigating = false };
    }

    private static StyledFrame Frame(AppState state)
    {
        return FrameBuilder.BuildStyled(state with { ViewportColumns = 120 }, 120, 36, _keymap)!;
    }

    // The screen cell of the text (first occurrence on a row that has a text region) plus an offset in it.
    private static ScreenCell Cell(StyledFrame frame, string text, int offset)
    {
        var lines = frame.PlainLines();
        var region = frame.Regions
            .Where(candidate => candidate.Target == MouseTarget.EditorText)
            .First(candidate => lines[candidate.Top][candidate.Left..].StartsWith(text, StringComparison.Ordinal)
                || lines[candidate.Top][candidate.Left..].Contains(text, StringComparison.Ordinal));

        return new ScreenCell(lines[region.Top].IndexOf(text, region.Left, StringComparison.Ordinal) + offset, region.Top);
    }

    private static AppState Gesture(AppState state, StyledFrame frame, string text, int offset, MouseGesture gesture)
    {
        var cell = Cell(frame, text, offset);

        return RegionProbe.At(state, frame, cell.Column, cell.Row, gesture)!.State;
    }

    private static AppState Drag(
        AppState state,
        StyledFrame frame,
        ScreenCell from,
        ScreenCell to,
        bool live)
    {
        var height = frame.Header.Count + frame.Body.Count + 1;
        var width = frame.Body[0].PlainText.Length;
        var context = MouseContext.From(state, width, height, frame.Header.Count, _at, frame.Regions);
        var commands = live
            ? MouseResolver.ResolveDragMove(context, from.Column, from.Row, to.Column, to.Row, false)
            : MouseResolver.ResolveDrag(context, from.Column, from.Row, to.Column, to.Row, _at);

        return new AppReducer(_keymap).Reduce(state, Assert.Single(commands)).State;
    }

    private static FormState FormOf(AppState state)
    {
        return state.Session.Tabs.Select(static tab => tab.Form).OfType<FormState>().Single();
    }

    private readonly record struct ScreenCell(int Column, int Row);
}
