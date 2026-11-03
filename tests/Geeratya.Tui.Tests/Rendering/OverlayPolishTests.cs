using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class OverlayPolishTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData(0, 5, 0, 12, " ")]
    [InlineData(0, 5, 3, 12, "▲")]
    [InlineData(4, 5, 3, 12, "▼")]
    [InlineData(4, 5, 7, 12, " ")]
    [InlineData(2, 5, 3, 12, " ")]
    [InlineData(0, 5, 0, 3, " ")]
    public void GivenAScrolledList_WhenAskingForTheMarker_ThenOnlyTheEdgeRowsWithHiddenRowsShowOne(
        int row,
        int area,
        int first,
        int total,
        string expected)
    {
        Assert.Equal(expected, OverlayBox.ScrollMarker(row, area, first, total));
    }

    [Theory]
    [InlineData(0, "▼")]
    [InlineData(3, "▲")]
    public void GivenTheHelpPage_WhenBuildingTheFrame_ThenTheRightColumnShowsTheHiddenRowMarkers(int scroll, string marker)
    {
        var state = AppState.Initial with { Mode = InputMode.Help, HelpScroll = scroll };

        var lines = FrameBuilder.Build(state, 80, 24, _keymap);

        Assert.Contains(lines, line => line.Contains($"{marker}│", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAFooterText_WhenBuildingHints_ThenKeysAreKeyRoleAndLabelsMutedWithTheGapsKept()
    {
        var line = OverlayBox.Hints("Up/Down move   Enter run  Esc cancel");

        var keys = line.Spans.Where(span => span.Role == TextRole.Key).Select(span => span.Text);

        Assert.Equal("Up/Down move   Enter run  Esc cancel", line.PlainText);
        Assert.Equal(["Up/Down", "Enter", "Esc"], keys);
    }

    [Fact]
    public void GivenManyJumpRows_WhenBuildingTheFrame_ThenTheBoxStopsAtEighteenRowsAndShowsTheDownMarker()
    {
        var state = JumpOverlaySnapshotTests.Sample(string.Empty);
        var tab = state.Session.Tabs[0];
        var rows = Enumerable.Range(1, 40).Select(number => tab.Rows[0] with { Key = $"GCM-{number}" }).ToList();
        var many = state with { Session = state.Session with { Tabs = [tab with { Rows = rows }] } };

        var frame = FrameBuilder.BuildStyled(many, 120, 50, _keymap)!;
        var box = frame.Regions.Single(region => region.Target == MouseTarget.OverlayBox);

        Assert.Contains(frame.PlainLines(), line => line.Contains('▼'));
        Assert.Equal(18, box.Height);
    }

    [Fact]
    public void GivenATransition_WhenAskingBeforeItRuns_ThenTheConfirmIsSevereAndNamesTheIssue()
    {
        var target = new IssueStatus("10005", "Done", "done");
        var choice = new TransitionChoice("41", "Done", target, false, false, []);
        var request = new TransitionRequest("GCM-8", choice, [], true);

        Assert.True(request.AskSevere);
        Assert.Equal("Move GCM-8", request.AskHeading);
    }

    [Fact]
    public void GivenALongAndAShortQuestion_WhenLayingOutTheDialog_ThenTheBoxShrinksToContentUpToTheMaximum()
    {
        var shortOne = ConfirmDialog.Layout(new ConfirmRequest("Quit?", "x", Label: "Quit"), 120, 36)!;
        var text = string.Concat(Enumerable.Repeat("word ", 20));
        var longOne = ConfirmDialog.Layout(new ConfirmRequest(text, "x"), 120, 36)!;

        Assert.Equal(30, shortOne.BoxWidth);
        Assert.InRange(longOne.BoxWidth, 55, ConfirmDialog.MaxBoxWidth);
    }

    [Fact]
    public void GivenAHeadingAndSeverity_WhenDrawingTheDialog_ThenTheChipNamesTheTargetAndTheFrameIsBad()
    {
        var request = new ConfirmRequest("Move GCM-8 to Done?", "x", Heading: "Move GCM-8", Destructive: true);

        var layout = ConfirmDialog.Layout(request, 80, 24)!;

        Assert.Equal("Move GCM-8", layout.Title);
        Assert.Equal(OverlayKind.Destructive, ConfirmOverlay.KindOf(request));
    }
}
