using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class StatusMessageRenderingTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(StatusSeverity.Info, " NORMAL  Saved A-1")]
    [InlineData(StatusSeverity.Warning, " NORMAL  Warning: Saved A-1")]
    [InlineData(StatusSeverity.Error, " NORMAL  Error: Saved A-1")]
    public void GivenAMessage_WhenRenderingTheStatusLine_ThenTheSeverityIsATextPrefixNotOnlyAColour(
        StatusSeverity severity,
        string expected)
    {
        var line = StatusBar.Render(Posted("Saved A-1", severity), 100, _keymap);

        Assert.StartsWith(expected, line);
        Assert.Equal(100, line.Length);
    }

    [Theory]
    [InlineData(StatusSeverity.Info, 1600, PaletteRole.Success)]
    [InlineData(StatusSeverity.Info, 1499, PaletteRole.SuccessChip)]
    [InlineData(StatusSeverity.Info, 0, PaletteRole.SuccessChip)]
    [InlineData(StatusSeverity.Warning, 0, PaletteRole.Warning)]
    [InlineData(StatusSeverity.Warning, 1600, PaletteRole.Warning)]
    [InlineData(StatusSeverity.Error, 0, PaletteRole.Error)]
    [InlineData(StatusSeverity.Error, 1600, PaletteRole.Error)]
    public void GivenAMessage_WhenBuildingTheFrame_ThenTheRoleFollowsSeverityAndFreshness(
        StatusSeverity severity,
        int ageMs,
        PaletteRole role)
    {
        var segment = StatusBar.MessageSegment(Posted("Saved A-1", severity), 100, _now.AddMilliseconds(ageMs));

        Assert.Equal(role, segment?.Role);
    }

    [Fact]
    public void GivenAnInfoMessage_WhenBuildingTheFrameWithoutAClock_ThenItIsTheSettledSuccessRole()
    {
        Assert.Equal(PaletteRole.Success, StatusBar.MessageSegment(Posted("Saved A-1", StatusSeverity.Info), 100)?.Role);
    }

    [Theory]
    [InlineData(StatusSeverity.Info, 0, PaletteRole.SuccessChip)]
    [InlineData(StatusSeverity.Info, 2000, PaletteRole.Success)]
    [InlineData(StatusSeverity.Warning, 0, PaletteRole.Warning)]
    [InlineData(StatusSeverity.Error, 2000, PaletteRole.Error)]
    public void GivenAMessage_WhenBuildingTheStyledFrame_ThenTheSegmentCarriesItsRoleAndText(
        StatusSeverity severity,
        int ageMs,
        PaletteRole role)
    {
        var now = _now.AddMilliseconds(ageMs);
        var frame = FrameBuilder.BuildStyled(Posted("Saved A-1", severity), 80, 24, _keymap, null, now);

        Assert.NotNull(frame?.StatusSegment);
        Assert.Equal(role, frame.StatusSegment.Role);
        Assert.EndsWith("Saved A-1", frame.StatusSegment.Text);
        Assert.StartsWith(StatusMessages.PrefixOf(severity), frame.StatusSegment.Text);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1499, true)]
    [InlineData(1500, false)]
    public void GivenAPostedMessage_WhenAskingIfItIsFresh_ThenItIsFreshForOneAndAHalfSeconds(int ageMs, bool fresh)
    {
        Assert.Equal(fresh, StatusMessages.IsFresh(Posted("x", StatusSeverity.Info), _now.AddMilliseconds(ageMs)));
        Assert.False(StatusMessages.IsFresh(AppState.Initial, _now));
    }

    [Fact]
    public void GivenTheChipAndTheSuccessRole_WhenResolvingWithoutColour_ThenBothDifferFromBodyAndEachOther()
    {
        var body = Palette.Resolve(BuiltInThemes.Default, ColorSupport.None, PaletteRole.Body).Style;
        var chip = Palette.Resolve(BuiltInThemes.Default, ColorSupport.None, PaletteRole.SuccessChip).Style;
        var success = Palette.Resolve(BuiltInThemes.Default, ColorSupport.None, PaletteRole.Success).Style;

        Assert.NotEqual(body, chip);
        Assert.NotEqual(body, success);
        Assert.NotEqual(success, chip);
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

        var lines = FrameBuilder.Build(state, 60, 12, _keymap);

        var heading = lines.ToList().FindIndex(line => line.StartsWith("Messages"));

        Assert.True(heading >= 0);
        Assert.StartsWith("Error: Second failed", lines[heading + 1]);
        Assert.StartsWith("First", lines[heading + 2]);
        Assert.StartsWith(" MESSAGES", lines[^1]);
    }

    [Fact]
    public void GivenNoMessages_WhenOpeningTheHistory_ThenItSaysSo()
    {
        var lines = FrameBuilder.Build(StatusMessages.OpenHistory(AppState.Initial), 60, 12, _keymap);

        Assert.Contains(lines, line => line.StartsWith("No messages yet."));
    }

    [Fact]
    public void GivenAnErrorAndPendingKeys_WhenAskingForTheSegmentAndCells_ThenNeitherCoversTheKeyEcho()
    {
        var state = Posted("Saved A-1", StatusSeverity.Error) with { PendingKeys = "Space" };

        Assert.Null(StatusBar.MessageSegment(state, 100));
        Assert.Null(StatusMessages.Cells(state));
    }

    [Fact]
    public void GivenAMessageAndPendingKeys_WhenRenderingTheStatusLine_ThenTheKeysEchoInsteadOfTheMessage()
    {
        var state = Posted("Saved A-1", StatusSeverity.Error) with { PendingKeys = "Space" };

        var line = StatusBar.Render(state, 100, _keymap);

        Assert.DoesNotContain("Saved A-1", line);
        Assert.Contains("<space>", line);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(12)]
    [InlineData(10)]
    public void GivenANarrowTerminal_WhenLocatingTheMessage_ThenTheSegmentAndTheClickStayInsideTheWidth(int width)
    {
        var state = Posted("A message that is far too long for this terminal", StatusSeverity.Error);
        var segment = StatusBar.MessageSegment(state, width);
        var context = MouseContext.From(state, width, 24, 2, _now);
        var cells = context.StatusMessage!.Value;

        Assert.NotNull(segment);
        Assert.True(segment.Start + segment.Width <= width);

        var inside = MouseResolver.Resolve(context, cells.Start, 23, MouseGesture.Click, _now);
        var atLastCell = MouseResolver.Resolve(context, width - 1, 23, MouseGesture.Click, _now);

        Assert.Equal(ActionIds.StatusHistory, Assert.IsType<ActionRequested>(Assert.Single(inside)).Action);
        Assert.Equal(ActionIds.StatusHistory, Assert.IsType<ActionRequested>(Assert.Single(atLastCell)).Action);
    }

    [Fact]
    public void GivenTheTerminalIsNarrowerThanTheBadge_WhenAskingForTheMessageSegment_ThenThereIsNone()
    {
        var state = Posted("Saved A-1", StatusSeverity.Error);

        Assert.Null(StatusBar.MessageSegment(state, StatusMessages.TextStart));
    }

    private static AppState Posted(string text, StatusSeverity severity)
    {
        var reducer = new AppReducer(_keymap);
        var state = AppState.Initial with { Message = text, MessageSeverity = severity };

        return reducer.Reduce(state, new Tick(_now)).State;
    }

    [Theory]
    [InlineData(ColorSupport.TrueColor)]
    [InlineData(ColorSupport.Basic16)]
    public void GivenAColourTier_WhenResolvingTheChip_ThenItIsFilledUnlikeTheSettledText(ColorSupport tier)
    {
        foreach (var name in BuiltInThemes.Names)
        {
            var theme = BuiltInThemes.Find(name)!.Theme!;
            var chip = Palette.Resolve(theme, tier, PaletteRole.SuccessChip);
            var success = Palette.Resolve(theme, tier, PaletteRole.Success);
            var body = Palette.Resolve(theme, tier, PaletteRole.Body);

            Assert.NotEqual(success.Background, chip.Background);
            Assert.NotEqual(body.Background, chip.Background);
            Assert.NotEqual(chip.Foreground, chip.Background);
        }
    }
}
