using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ConfirmNoticeTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly AppReducer _reducer = new(_keymap);

    [Fact]
    public void GivenANotice_WhenBuildingTheFrame_ThenItHasTheTitleOneCloseRowAndCloseHint()
    {
        var frame = FrameBuilder.BuildStyled(Open("Ship it"), 80, 24, _keymap)!;
        var text = string.Join('\n', frame.PlainLines());

        Assert.Contains("Sprint goal", text, StringComparison.Ordinal);
        Assert.Contains("Ship it", text, StringComparison.Ordinal);
        Assert.Contains("y  Close", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancel", text, StringComparison.Ordinal);
        Assert.Contains("y close", text, StringComparison.Ordinal);
        Assert.DoesNotContain("n no", text, StringComparison.Ordinal);
        Assert.Contains("Sprint goal  (y/n close)", frame.StatusLine, StringComparison.Ordinal);
        Assert.DoesNotContain(frame.Regions, region => region.Target == MouseTarget.ConfirmNo);
    }

    [Fact]
    public void GivenANotice_WhenClickingTheCloseRowAsDrawn_ThenItClosesWithoutAMessage()
    {
        var state = Open("Ship it");
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var lines = frame.PlainLines();
        var row = lines.ToList().FindIndex(line => line.Contains("y  Close", StringComparison.Ordinal));
        var column = lines[row].IndexOf("y  Close", StringComparison.Ordinal);
        var context = MouseContext.From(state, 80, 24, frame.Header.Count, _at, frame.Regions);

        var commands = MouseResolver.Resolve(context, column, row, MouseGesture.Click, _at);
        var result = _reducer.Reduce(state, Assert.Single(commands));

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(result.State.Message);
    }

    [Fact]
    public void GivenANotice_WhenClickingOutsideTheBox_ThenItClosesWithoutCancelledMessage()
    {
        var state = Open("Ship it");
        var frame = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!;
        var context = MouseContext.From(state, 80, 24, frame.Header.Count, _at, frame.Regions);

        var commands = MouseResolver.Resolve(context, 0, 5, MouseGesture.Click, _at);
        var result = _reducer.Reduce(state, Assert.Single(commands));

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(result.State.Message);
    }

    [Fact]
    public void GivenATextTallerThanTheBody_WhenLayingOut_ThenItIsCutWithAnEllipsisInsteadOfRefused()
    {
        var lines = Enumerable.Range(1, 40).Select(number => $"line {number}");
        var request = new ConfirmRequest(string.Join('\n', lines), "x", Title: "T");

        var layout = ConfirmDialog.Layout(request, 60, 12)!;

        Assert.Equal(8, layout.Question.Count);
        Assert.EndsWith("…", layout.Question[^1], StringComparison.Ordinal);
        Assert.True(layout.Height <= 12);
    }

    private static AppState Open(string goal)
    {
        var request = new ConfirmRequest($"Sprint 4\n\n{goal}", ActionIds.Cancel, Label: "Close", Title: "Sprint goal");

        return _reducer.Reduce(AppState.Initial, new ConfirmRequested(request)).State;
    }
}
