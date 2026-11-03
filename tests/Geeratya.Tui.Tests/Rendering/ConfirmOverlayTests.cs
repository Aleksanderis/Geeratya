using Geeratya.Core.Comments;
using Geeratya.Core.Editing;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ConfirmOverlayTests
{
    private const string Question = "Delete the GCM-3 comment? Jira cannot undo this. By Eve: \"first words\"";

    private static readonly DateTimeOffset _at = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);
    private static readonly AppReducer _reducer = new(_keymap);

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    public void GivenRoom_WhenBuildingTheFrame_ThenTheDialogNamesWhatChangesAndTheStatusLineIsShort(int width, int height)
    {
        var frame = FrameBuilder.BuildStyled(Open(), width, height, _helpLines, _keymap)!;
        var text = string.Join('\n', frame.PlainLines());

        Assert.Contains("Delete the GCM-3 comment?", text, StringComparison.Ordinal);
        Assert.Contains("Jira cannot undo this.", text, StringComparison.Ordinal);
        Assert.Contains("By Eve: \"first words\"", text, StringComparison.Ordinal);
        Assert.Contains("y  Delete comment", text, StringComparison.Ordinal);
        Assert.Contains("n  Cancel", text, StringComparison.Ordinal);
        Assert.Contains("CONFIRM Confirm  (y/n)", frame.StatusLine, StringComparison.Ordinal);
        Assert.DoesNotContain("Delete the GCM-3", frame.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenALongQuestionAtEightyColumns_WhenBuildingTheFrame_ThenNothingIsCutOff()
    {
        var message = "Remove profile work (me@work.test on https://work.atlassian.net) and delete its stored token? "
            + "Two writes are running. This cannot be undone";
        var state = Open(new ConfirmRequest(message, "x", Label: "Remove profile"));

        var lines = FrameBuilder.BuildStyled(state, 80, 24, _helpLines, _keymap)!.PlainLines();
        var text = string.Join(' ', lines.Select(line => line.Replace('│', ' ')));

        Assert.Contains("https://work.atlassian.net)", text, StringComparison.Ordinal);
        Assert.Contains("This cannot be undone", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTooLittleRoom_WhenBuildingTheFrame_ThenNoDialogIsDrawnAndTheStatusLineKeepsTheQuestion()
    {
        var frame = FrameBuilder.BuildStyled(Open(), 40, 10, _helpLines, _keymap)!;

        Assert.Empty(frame.Regions);
        Assert.DoesNotContain("┌─ Confirm", string.Join('\n', frame.PlainLines()), StringComparison.Ordinal);
        Assert.Contains("Delete the GCM-3", frame.StatusLine, StringComparison.Ordinal);
        Assert.EndsWith("(y/n)", frame.StatusLine.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnySize_WhenBuildingTheFrame_ThenItNeverThrowsAndRegionsStayOnTheScreen()
    {
        for (var width = 0; width <= 90; width += 3)
        {
            for (var height = 0; height <= 30; height++)
            {
                var frame = FrameBuilder.BuildStyled(Open(), width, height, _helpLines, _keymap);

                foreach (var region in frame?.Regions ?? [])
                {
                    Assert.InRange(region.Top, 0, height - 1);
                    Assert.InRange(region.Left + region.Width, 1, width);
                }
            }
        }
    }

    [Fact]
    public void GivenTheDialog_WhenClickingTheYesRow_ThenTheActionRunsAndTheModeIsRestored()
    {
        var state = Open(new ConfirmRequest("Quit anyway?", ActionIds.Quit, Label: "Quit"));
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _helpLines, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.ConfirmYes).Single();

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        Assert.Contains("y  Quit", row.Text, StringComparison.Ordinal);
        Assert.IsType<QuitEffect>(Assert.Single(result.Effects));
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(result.State.Confirm);
    }

    [Fact]
    public void GivenTheDialog_WhenClickingTheNoRow_ThenNothingRunsAndTheDialogCloses()
    {
        var state = Open();
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _helpLines, _keymap)!;
        var row = RegionProbe.Rows(frame, MouseTarget.ConfirmNo).Single();

        var result = RegionProbe.Click(state, frame, row.Region, MouseGesture.Click)!;

        Assert.Contains("n  Cancel", row.Text, StringComparison.Ordinal);
        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Equal("Cancelled.", result.State.Message);
    }

    [Fact]
    public void GivenTheDialog_WhenClickingOutsideTheBoxInTheBodyOrTheStatusLine_ThenItCancelsLikeN()
    {
        var state = Open();
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _helpLines, _keymap)!;
        var context = MouseContext.From(state, 80, 24, frame.Header.Count, _at, frame.Regions);
        int[][] cells = [[0, 5], [79, 12], [3, 22], [10, 23]];

        foreach (var cell in cells)
        {
            var commands = MouseResolver.Resolve(context, cell[0], cell[1], MouseGesture.Click, _at);
            var result = _reducer.Reduce(state, Assert.Single(commands));

            Assert.Equal(InputMode.Normal, result.State.Mode);
            Assert.Empty(result.Effects);
        }
    }

    [Fact]
    public void GivenTheDialog_WhenClickingInsideTheBoxOffTheRows_ThenNothingHappens()
    {
        var state = Open();
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _helpLines, _keymap)!;
        var box = frame.Regions.Single(region => region.Target == MouseTarget.ConfirmBox);
        var context = MouseContext.From(state, 80, 24, frame.Header.Count, _at, frame.Regions);

        var title = MouseResolver.Resolve(context, box.Left + 3, box.Top + 1, MouseGesture.Click, _at);
        var edge = MouseResolver.Resolve(context, box.Left, box.Top + box.Height - 1, MouseGesture.Click, _at);

        Assert.Empty(title);
        Assert.Empty(edge);
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData("x")]
    [InlineData("j")]
    [InlineData("q")]
    [InlineData(":")]
    [InlineData("Ctrl-c")]
    public void GivenTheDialog_WhenPressingAnyOtherKey_ThenItStaysOpenAndNothingRuns(string key)
    {
        var state = Open();

        var result = _reducer.Reduce(state, new KeyPressed(key, _at));

        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Same(state.Confirm, result.State.Confirm);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenTheDialogOpen_WhenTheTerminalShrinksBelowIt_ThenItClosesUnansweredWithAWarning()
    {
        var state = Open() with { ViewportColumns = 80, ViewportRows = 21 };

        var result = _reducer.Reduce(state, new ViewportChanged(7, 40));

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(result.State.Confirm);
        Assert.Empty(result.Effects);
        Assert.Contains("nothing was done", result.State.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheDialogOpen_WhenTheTerminalStillFits_ThenItStaysOpen()
    {
        var state = Open() with { ViewportColumns = 80, ViewportRows = 21 };

        var result = _reducer.Reduce(state, new ViewportChanged(30, 100));

        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.NotNull(result.State.Confirm);
    }

    [Fact]
    public void GivenATinyTerminal_WhenAConfirmIsRequested_ThenItIsNotOpenedAndTheStatusSaysSo()
    {
        var state = AppState.Initial with { ViewportColumns = 40, ViewportRows = 7 };

        var result = _reducer.Reduce(state, new ConfirmRequested(new ConfirmRequest(Question, "x")));

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(result.State.Confirm);
        Assert.Contains("too small to confirm", result.State.Message, StringComparison.Ordinal);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenADiscardQuestionOverTheEditor_WhenTheTerminalShrinks_ThenTheEditorGetsItsTextBack()
    {
        var editor = new CommentEditor(new TabId(1), "A-1", null, TextBuffer.Of("draft"), string.Empty);
        var request = new ConfirmRequest("Discard the unsent comment text? It is not saved anywhere", "x");
        var state = Open(request) with { ViewportColumns = 80, ViewportRows = 21, Editor = editor };

        var result = _reducer.Reduce(state, new ViewportChanged(5, 80));

        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Same(editor, result.State.Editor);
        Assert.Null(result.State.Confirm);
    }

    private static AppState Open(ConfirmRequest? request = null)
    {
        return AppState.Initial with
        {
            Mode = InputMode.Confirm,
            Confirm = request ?? new ConfirmRequest(Question, ActionIds.CommentDelete, "1", Label: "Delete comment"),
        };
    }
}
