using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class StatusMessageRenderingTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(_keymap);
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(StatusSeverity.Info, " NORMAL Saved A-1")]
    [InlineData(StatusSeverity.Warning, " NORMAL Warning: Saved A-1")]
    [InlineData(StatusSeverity.Error, " NORMAL Error: Saved A-1")]
    public void GivenAMessage_WhenRenderingTheStatusLine_ThenTheSeverityIsATextPrefixNotOnlyAColour(
        StatusSeverity severity,
        string expected)
    {
        var line = StatusBar.Render(Posted("Saved A-1", severity), 100, _keymap);

        Assert.StartsWith(expected, line);
        Assert.Equal(100, line.Length);
    }

    [Theory]
    [InlineData(StatusSeverity.Info, null)]
    [InlineData(StatusSeverity.Warning, PaletteRole.Warning)]
    [InlineData(StatusSeverity.Error, PaletteRole.Error)]
    public void GivenAMessage_WhenBuildingTheFrame_ThenOnlyWarningsAndErrorsGetTheirOwnRole(
        StatusSeverity severity,
        PaletteRole? role)
    {
        var segment = StatusBar.MessageSegment(Posted("Saved A-1", severity), 100);

        Assert.Equal(role, segment?.Role);
    }

    [Fact]
    public void GivenAMessage_WhenLocatingItsCells_ThenTheyMatchWhereTheStatusLineDrawsIt()
    {
        var state = Posted("Saved A-1", StatusSeverity.Error);
        var line = StatusBar.Render(state, 100, _keymap);
        var cells = StatusMessages.Cells(state)!.Value;

        Assert.Equal("Error: Saved A-1", line[cells.Start..cells.End]);
    }

    [Fact]
    public void GivenAMessage_WhenClickingItAndTheRestOfTheLine_ThenOnlyTheMessageOpensTheHistory()
    {
        var state = Posted("Saved A-1", StatusSeverity.Info);
        var context = MouseContext.From(state, 100, 24, 2, _now);
        var cells = context.StatusMessage!.Value;

        var onMessage = MouseResolver.Resolve(context, cells.Start, 23, MouseGesture.Click, _now);
        var elsewhere = MouseResolver.Resolve(context, cells.End + 1, 23, MouseGesture.Click, _now);

        Assert.Equal(ActionIds.StatusHistory, Assert.IsType<ActionRequested>(Assert.Single(onMessage)).Action);
        Assert.Equal(ActionIds.PaletteOpen, Assert.IsType<ActionRequested>(Assert.Single(elsewhere)).Action);
    }

    [Fact]
    public void GivenTheHistoryOverlay_WhenBuildingTheFrame_ThenItListsTheMessagesNewestFirstWithTheirPrefix()
    {
        var reducer = new AppReducer(_keymap);
        var state = Posted("First", StatusSeverity.Info);
        state = reducer.Reduce(StatusMessages.Fail(state, "Second failed"), new KeyPressed("x", _now)).State;
        state = StatusMessages.OpenHistory(state);

        var lines = FrameBuilder.Build(state, 60, 12, _helpLines, _keymap);

        var heading = lines.ToList().FindIndex(line => line.StartsWith("Messages"));

        Assert.True(heading >= 0);
        Assert.StartsWith("Error: Second failed", lines[heading + 1]);
        Assert.StartsWith("First", lines[heading + 2]);
        Assert.StartsWith(" MESSAGES", lines[^1]);
    }

    [Fact]
    public void GivenNoMessages_WhenOpeningTheHistory_ThenItSaysSo()
    {
        var lines = FrameBuilder.Build(StatusMessages.OpenHistory(AppState.Initial), 60, 12, _helpLines, _keymap);

        Assert.Contains(lines, line => line.StartsWith("No messages yet."));
    }

    [Fact]
    public void GivenAMessageAndPendingKeys_WhenRenderingTheStatusLine_ThenTheKeysEchoInsteadOfTheMessage()
    {
        var state = Posted("Saved A-1", StatusSeverity.Error) with { PendingKeys = "Space" };

        var line = StatusBar.Render(state, 100, _keymap);

        Assert.DoesNotContain("Saved A-1", line);
        Assert.Contains("Space", line);
    }

    private static AppState Posted(string text, StatusSeverity severity)
    {
        var reducer = new AppReducer(_keymap);
        var state = AppState.Initial with { Message = text, MessageSeverity = severity };

        return reducer.Reduce(state, new Tick(_now)).State;
    }
}
