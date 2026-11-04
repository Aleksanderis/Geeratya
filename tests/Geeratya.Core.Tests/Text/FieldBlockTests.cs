using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Text;

/// <summary>The filled field block widget the Details pane and (later) the forms share.</summary>
[Trait("Os", "Independent")]
public class FieldBlockTests
{
    [Fact]
    public void GivenALabelAndAValue_WhenBuildingRows_ThenTheLabelRowComesFirstAndTheValueRowsFillTheWidth()
    {
        var spec = FieldBlockSpec.OfValue("Priority", [new StyledSpan("High", TextRole.ValueBlock)], false);

        var rows = FieldBlock.Rows(spec, 20);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Priority", rows[0].PlainText);
        Assert.Equal(TextRole.FieldLabel, rows[0].Spans[0].Role);
        Assert.Equal(" High", rows[1].PlainText.TrimEnd());
        Assert.Equal(20, DisplayWidth.Of(rows[1].PlainText));
        Assert.All(rows[1].Spans, span => Assert.Equal(TextRole.ValueBlock, span.Role));
    }

    [Fact]
    public void GivenANote_WhenBuildingRows_ThenItFollowsTheLabelInTheMutedRole()
    {
        var value = new StyledSpan("To Do", TextRole.ValueBlock);
        var spec = FieldBlockSpec.OfValue("Status", [value], true, "moves via transition");

        var label = FieldBlock.Rows(spec, 30)[0];

        Assert.Equal("Status  moves via transition", label.PlainText);
        Assert.Equal(TextRole.Muted, label.Spans[^1].Role);
    }

    [Fact]
    public void GivenAnEditableBlock_WhenBuildingRows_ThenTheLastValueRowEndsInTheDropdownMark()
    {
        var spec = FieldBlockSpec.OfValue("Priority", [new StyledSpan("High", TextRole.ValueBlock)], true);

        var value = FieldBlock.Rows(spec, 20)[1].PlainText;

        Assert.Equal(" High ▾", value.TrimEnd());
        Assert.Equal(20, DisplayWidth.Of(value));
    }

    [Fact]
    public void GivenAReadOnlyBlock_WhenBuildingRows_ThenThereIsNoDropdownMark()
    {
        var spec = FieldBlockSpec.OfValue("Reporter", [new StyledSpan("Ola", TextRole.ValueBlock)], false);

        Assert.DoesNotContain("▾", FieldBlock.Rows(spec, 20)[1].PlainText);
    }

    [Fact]
    public void GivenManyItems_WhenTheyDoNotFit_ThenTheyWrapAtItemBoundariesAndEveryRowKeepsTheWidth()
    {
        var items = Enumerable.Range(1, 6)
            .Select(number => (IReadOnlyList<StyledSpan>)[new StyledSpan($"[label-{number}]", TextRole.LabelChip0)])
            .ToList();
        var spec = new FieldBlockSpec("Labels", items, true);

        var rows = FieldBlock.Rows(spec, 24);

        Assert.True(rows.Count > 2);
        Assert.All(rows.Skip(1), row => Assert.Equal(24, DisplayWidth.Of(row.PlainText)));
        Assert.All(
            rows.Skip(1),
            row => Assert.Equal(row.PlainText.Count(static c => c == '['), row.PlainText.Count(static c => c == ']')));
        Assert.EndsWith("▾", rows[^1].PlainText.TrimEnd());
        Assert.Single(rows, row => row.PlainText.Contains('▾'));
    }

    [Fact]
    public void GivenAnItemWiderThanTheBlock_WhenBuildingRows_ThenItIsCutWithAnEllipsis()
    {
        var spec = FieldBlockSpec.OfValue("Sprint", [new StyledSpan(new string('x', 40), TextRole.ValueBlock)], true);

        var value = FieldBlock.Rows(spec, 16)[1].PlainText;

        Assert.Equal(16, DisplayWidth.Of(value));
        Assert.Contains("…", value);
        Assert.EndsWith("▾", value.TrimEnd());
    }

    [Fact]
    public void GivenWideCharacters_WhenBuildingRows_ThenNoRowIsWiderThanTheBlock()
    {
        var spec = FieldBlockSpec.OfValue("Assignee", [new StyledSpan("日本語の担当者の名前がとても長い", TextRole.ValueBlock)], true);

        var rows = FieldBlock.Rows(spec, 15);

        Assert.All(rows.Skip(1), row => Assert.Equal(15, DisplayWidth.Of(row.PlainText)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GivenATinyWidth_WhenBuildingRows_ThenItNeverThrowsAndNoRowIsWider(int width)
    {
        var spec = FieldBlockSpec.OfValue("Status", [new StyledSpan("In Progress", TextRole.ValueBlock)], true, "note");

        var rows = FieldBlock.Rows(spec, width);

        Assert.All(rows, row => Assert.True(DisplayWidth.Of(row.PlainText) <= Math.Max(width, 3)));
    }

    [Fact]
    public void GivenAValueRow_WhenFocusing_ThenTheLeadBecomesTheBarAndTheTintTheSelection()
    {
        var spec = FieldBlockSpec.OfValue("Priority", [new StyledSpan("High", TextRole.ValueBlock)], true);
        var row = FieldBlock.Rows(spec, 20)[1];

        var focused = FieldBlock.Focus(row, false);

        Assert.Equal(FieldBlock.Bar, focused.Spans[0].Text);
        Assert.Equal(TextRole.SelectionBar, focused.Spans[0].Role);
        Assert.Equal(DisplayWidth.Of(row.PlainText), DisplayWidth.Of(focused.PlainText));
        Assert.DoesNotContain(focused.Spans, span => span.Role == TextRole.ValueBlock);
        Assert.Contains(focused.Spans, span => span.Role == TextRole.Selected && span.Text.Contains("High"));
    }

    [Fact]
    public void GivenTheAsciiSet_WhenFocusing_ThenTheBarIsAGreaterThanSign()
    {
        var spec = FieldBlockSpec.OfValue("Priority", [new StyledSpan("High", TextRole.ValueBlock)], false);

        var focused = FieldBlock.Focus(FieldBlock.Rows(spec, 20)[1], true);

        Assert.Equal(">", focused.Spans[0].Text);
    }

    [Fact]
    public void GivenChipsInTheValue_WhenFocusing_ThenTheyKeepTheirOwnRole()
    {
        var spec = FieldBlockSpec.OfValue("Assignee", [new StyledSpan("[GW]", TextRole.InitialsChip)], false);

        var focused = FieldBlock.Focus(FieldBlock.Rows(spec, 20)[1], false);

        Assert.Contains(focused.Spans, span => span.Role == TextRole.InitialsChip && span.Text == "[GW]");
    }

    [Fact]
    public void GivenTheLabelRow_WhenFocusing_ThenItIsReturnedAsItIs()
    {
        var spec = FieldBlockSpec.OfValue("Priority", [new StyledSpan("High", TextRole.ValueBlock)], false);
        var label = FieldBlock.Rows(spec, 20)[0];

        Assert.Same(label, FieldBlock.Focus(label, false));
    }
}
