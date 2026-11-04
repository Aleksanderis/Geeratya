using Geeratya.Core.Boards;
using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class CardLabelLayoutTests
{
    [Fact]
    public void GivenNoLabels_WhenLayingOut_ThenThereAreNoRows()
    {
        Assert.Empty(CardLabelLayout.Rows([], 30));
    }

    [Fact]
    public void GivenLabelsThatFitOneRow_WhenLayingOut_ThenThereIsOneRowWithoutRest()
    {
        var rows = CardLabelLayout.Rows(["alpha", "beta"], 40);

        var row = Assert.Single(rows);
        Assert.Equal([" alpha ", " beta "], row.Chips.Select(static chip => chip.Text));
        Assert.Equal(0, row.Hidden);
    }

    [Fact]
    public void GivenLabelsThatOverflowTheFirstRow_WhenLayingOut_ThenTheyWrapToASecondRow()
    {
        var rows = CardLabelLayout.Rows(["alpha", "beta", "gamma", "delta"], 22);

        Assert.Equal(2, rows.Count);
        Assert.Equal([" alpha ", " beta ", " gamma "], rows[0].Chips.Select(static chip => chip.Text));
        Assert.Equal([" delta "], rows[1].Chips.Select(static chip => chip.Text));
    }

    [Fact]
    public void GivenMoreLabelsThanTwoRowsHold_WhenLayingOut_ThenTheLastRowEndsWithTheHiddenCount()
    {
        var rows = CardLabelLayout.Rows(["alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta"], 22);

        Assert.Equal(2, rows.Count);
        Assert.True(rows[1].Hidden > 0);
        Assert.Equal(7, rows.Sum(static row => row.Chips.Count) + rows[1].Hidden);
        Assert.True(Width(rows[1]) <= 22);
    }

    [Fact]
    public void GivenALabelWiderThanTheRow_WhenLayingOut_ThenItIsCutWithAnEllipsis()
    {
        var rows = CardLabelLayout.Rows(["a-very-long-label-name"], 12);

        Assert.Equal(" a-very-lo… ", Assert.Single(Assert.Single(rows).Chips).Text);
    }

    [Fact]
    public void GivenARowTooNarrowForAnyChip_WhenLayingOut_ThenOneRowWithTheHiddenCountRemains()
    {
        var rows = CardLabelLayout.Rows(["alpha", "beta"], 3);

        var row = Assert.Single(rows);
        Assert.Empty(row.Chips);
        Assert.Equal(2, row.Hidden);
    }

    [Fact]
    public void GivenASecondRowThatCannotHoldAChip_WhenLayingOut_ThenTheHiddenCountStaysOnTheLastFilledRow()
    {
        var rows = CardLabelLayout.Rows(["aaaa", "bbbb", "cccc"], 6);

        var row = Assert.Single(rows);
        Assert.Equal([" aaaa "], row.Chips.Select(static chip => chip.Text));
        Assert.Equal(0, row.Hidden);
    }

    private static int Width(CardLabelRow row)
    {
        var chips = row.Chips.Sum(static chip => DisplayWidth.Of(chip.Text)) + Math.Max(0, row.Chips.Count - 1);

        return row.Hidden > 0 ? chips + 1 + DisplayWidth.Of($"+{row.Hidden}") : chips;
    }
}
