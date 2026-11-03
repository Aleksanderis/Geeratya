using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The message history overlay at the three snapshot sizes, with a wide-character message. To refresh after an
/// intended change, copy the actual frame from the failure into the matching file and review the diff.
/// </summary>
public class StatusMessageSnapshotTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    public void GivenTheHistoryOverlay_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var lines = FrameBuilder.Build(HistoryState(), width, height, _helpLines, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var path = Path.Combine(AppContext.BaseDirectory, "Snapshots", $"messages-{width}x{height}.txt");

        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    public void GivenTheSettingsList_WhenBuildingTheFrame_ThenItMatchesTheSnapshot(int width, int height)
    {
        var state = StatusMessages.OpenSettings(Posted());
        var lines = FrameBuilder.Build(state, width, height, _helpLines, _keymap);
        var actual = string.Join('\n', lines) + "\n";
        var expected = SnapshotFile.Expect($"messages-settings-{width}x{height}.txt", actual);

        Assert.Equal(expected.ReplaceLineEndings("\n"), actual);
    }

    private static AppState HistoryState()
    {
        var reducer = new AppReducer(_keymap);
        var state = Posted();

        state = reducer.Reduce(StatusMessages.Warn(state, "Rate limited, retrying"), new KeyPressed("x", _now)).State;
        state = reducer.Reduce(StatusMessages.Fail(state, "Could not save \u30c6\u30b9\u30c8"), new KeyPressed("x", _now)).State;

        return StatusMessages.OpenHistory(state);
    }

    private static AppState Posted()
    {
        var reducer = new AppReducer(_keymap);

        return reducer.Reduce(AppState.Initial with { Message = "Saved A-1" }, new Tick(_now)).State;
    }
}
