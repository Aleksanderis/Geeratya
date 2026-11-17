using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Text;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class AnswerRowsTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenTwoAnswers_WhenBuildingTheLines_ThenTheKeysShareOneColumnAndTheWordsAreStrong()
    {
        AnswerRow[] answers =
        [
            new(new HintChip("y", "Delete"), MouseTarget.ConfirmYes),
            new(new HintChip("Esc", "Cancel"), MouseTarget.ConfirmNo),
        ];

        var lines = AnswerRows.Lines(answers, 30);

        Assert.Equal("y    Delete", lines[0].PlainText);
        Assert.Equal("Esc  Cancel", lines[1].PlainText);
        Assert.All(lines, line => Assert.Equal(TextRole.Key, line.Spans[0].Role));
        Assert.All(lines, line => Assert.Equal(TextRole.Strong, line.Spans[2].Role));
    }

    [Fact]
    public void GivenALongLabel_WhenBuildingTheLines_ThenItIsCutToTheWidth()
    {
        AnswerRow[] answers = [new(new HintChip("y", "A very long action name"), MouseTarget.ConfirmYes)];

        var lines = AnswerRows.Lines(answers, 12);

        Assert.True(TextWidth.Of(lines[0].PlainText) <= 12);
    }

    [Fact]
    public void GivenNoAnswers_WhenBuildingTheLines_ThenThereAreNone()
    {
        Assert.Empty(AnswerRows.Lines([], 20));
    }

    [Fact]
    public void GivenAnswers_WhenMarking_ThenEachGetsItsOwnRowAndTargetOnTheBoxWidth()
    {
        var regions = new List<OverlayRegion>();
        AnswerRow[] answers =
        [
            new(new HintChip("y", "Go"), MouseTarget.ConfirmYes),
            new(new HintChip("n", "Cancel"), MouseTarget.ConfirmNo),
        ];

        AnswerRows.Mark(regions, new BoxPlace(5, 10, 40), 4, answers);

        Assert.Equal([MouseTarget.ConfirmYes, MouseTarget.ConfirmNo], regions.Select(region => region.Target));
        Assert.Equal([9, 10], regions.Select(region => region.Top));
        Assert.All(regions, region => Assert.Equal(38, region.Width));
    }

    [Fact]
    public void GivenTheConfirmDialog_WhenDrawn_ThenTheAnswerRowsAreTheLinesTheRegionsMark()
    {
        var frame = FrameBuilder.BuildStyled(ConfirmOverlayTests.Open(), 100, 30, _keymap)!;
        var lines = frame.PlainLines();
        var yes = frame.Regions.Single(region => region.Target == MouseTarget.ConfirmYes);
        var no = frame.Regions.Single(region => region.Target == MouseTarget.ConfirmNo);

        Assert.Contains("Delete comment", lines[yes.Top], StringComparison.Ordinal);
        Assert.Contains("Cancel", lines[no.Top], StringComparison.Ordinal);
        Assert.Equal(yes.Top + 1, no.Top);
    }
}
