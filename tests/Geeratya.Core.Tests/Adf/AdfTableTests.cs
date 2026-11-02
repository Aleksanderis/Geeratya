using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using static Geeratya.Core.Tests.Adf.AdfTestSupport;

namespace Geeratya.Core.Tests.Adf;

[Trait("Os", "Independent")]
public sealed class AdfTableTests
{
    private const string SmallTable = """
        {"type":"table","content":[
          {"type":"tableRow","content":[
            {"type":"tableHeader","attrs":{},"content":[
              {"type":"paragraph","content":[{"type":"text","text":"Name"}]}]},
            {"type":"tableHeader","attrs":{},"content":[
              {"type":"paragraph","content":[{"type":"text","text":"Qty"}]}]}]},
          {"type":"tableRow","content":[
            {"type":"tableCell","attrs":{},"content":[{"type":"paragraph","content":[{"type":"text","text":"apple"}]}]},
            {"type":"tableCell","attrs":{},"content":[{"type":"paragraph","content":[{"type":"text","text":"3"}]}]}]}]}
        """;

    private const string WideTable = """
        {"type":"table","content":[
          {"type":"tableRow","content":[
            {"type":"tableHeader","content":[{"type":"paragraph","content":[{"type":"text","text":"Key"}]}]},
            {"type":"tableHeader","content":[{"type":"paragraph","content":[{"type":"text","text":"Notes"}]}]}]},
          {"type":"tableRow","content":[
            {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"one"}]}]},
            {"type":"tableCell","content":[{"type":"paragraph","content":[
              {"type":"text","text":"alpha beta gamma delta epsilon zeta eta theta iota"}]}]}]}]}
        """;

    [Fact]
    public void GivenTableThatFits_WhenRendering_ThenColumnsAreSizedFromTheContentWithAHeaderRule()
    {
        var expected = new[]
        {
            "| Name  | Qty |",
            "|-------|-----|",
            "| apple | 3   |",
        };

        Assert.Equal(expected, Lines(SmallTable, 80));
    }

    [Fact]
    public void GivenHeaderCells_WhenRendering_ThenTheirTextIsStrong()
    {
        var rendering = AdfRenderer.Render(Document(SmallTable), 80);

        Assert.Contains(rendering.Lines[0].Spans, span => span is { Text: "Name", Role: TextRole.Strong });
        Assert.Contains(rendering.Lines[2].Spans, span => span is { Text: "apple", Role: TextRole.Body });
    }

    [Fact]
    public void GivenTableSlightlyWiderThanTheWidth_WhenRendering_ThenTheWidestColumnWrapsItsCells()
    {
        var expected = new[]
        {
            "| Key | Notes          |",
            "|-----|----------------|",
            "| one | alpha beta     |",
            "|     | gamma delta    |",
            "|     | epsilon zeta   |",
            "|     | eta theta iota |",
        };

        Assert.Equal(expected, Lines(WideTable, 24));
    }

    [Fact]
    public void GivenTableWiderThanTheMinimumColumnsAllow_WhenRendering_ThenItDegradesToOneBlockPerRow()
    {
        var expected = new[]
        {
            "Key: one",
            "Notes: alpha beta",
            "       gamma",
            "       delta",
            "       epsilon",
            "       zeta eta",
            "       theta iota",
        };

        Assert.Equal(expected, Lines(WideTable, 17));
    }

    [Fact]
    public void GivenCellWithColumnSpan_WhenRendering_ThenItCoversBothColumns()
    {
        const string json = """
            {"type":"table","content":[
              {"type":"tableRow","content":[
                {"type":"tableCell","attrs":{"colspan":2},"content":[
                  {"type":"paragraph","content":[{"type":"text","text":"wide cell here"}]}]}]},
              {"type":"tableRow","content":[
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"a"}]}]},
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"b"}]}]}]}]}
            """;

        var lines = Lines(json, 80);

        Assert.Equal(2, lines.Length);
        Assert.Equal(lines[0].Length, lines[1].Length);
        Assert.StartsWith("| wide cell here", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenShortRow_WhenRendering_ThenMissingCellsArePadded()
    {
        const string json = """
            {"type":"table","content":[
              {"type":"tableRow","content":[
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"a"}]}]},
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"b"}]}]}]},
              {"type":"tableRow","content":[
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"c"}]}]}]}]}
            """;

        Assert.Equal(["| a | b |", "| c |   |"], Lines(json, 80));
    }

    [Fact]
    public void GivenTableWithWideCharacters_WhenRendering_ThenRowsAlignByDisplayWidth()
    {
        const string json = """
            {"type":"table","content":[
              {"type":"tableRow","content":[
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"日本語"}]}]},
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"x"}]}]}]},
              {"type":"tableRow","content":[
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"abcdef"}]}]},
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"y"}]}]}]}]}
            """;

        Assert.Equal(["| 日本語 | x |", "| abcdef | y |"], Lines(json, 80));
    }

    [Fact]
    public void GivenHeaderOnlyTableTooNarrow_WhenRendering_ThenTheHeaderTextIsStillShown()
    {
        const string json = """
            {"type":"table","content":[{"type":"tableRow","content":[
              {"type":"tableHeader","content":[
                {"type":"paragraph","content":[{"type":"text","text":"AAAAAAAAAAAAAA"}]}]},
              {"type":"tableHeader","content":[
                {"type":"paragraph","content":[{"type":"text","text":"BBBBBBBBBBBBBBB"}]}]}
            ]}]}
            """;

        var lines = Lines(json, 12);

        Assert.Contains(lines, static line => line.Contains("AAAAAAAAAA", StringComparison.Ordinal));
        Assert.Contains(lines, static line => line.Contains("BBBBBBBBBB", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenVeryLongHeaderInStackedTable_WhenRendering_ThenTheLabelGetsItsOwnLineAboveTheValue()
    {
        const string json = """
            {"type":"table","content":[
              {"type":"tableRow","content":[
                {"type":"tableHeader","content":[
                  {"type":"paragraph","content":[{"type":"text","text":"Estimated effort in hours"}]}]},
                {"type":"tableHeader","content":[
                  {"type":"paragraph","content":[{"type":"text","text":"Other long one"}]}]}]},
              {"type":"tableRow","content":[
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"12"}]}]},
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"y"}]}]}]}]}
            """;

        var expected = new[] { "Estimated effort in", "hours", "  12", "Other long one", "  y" };

        Assert.Equal(expected, Lines(json, 20));
    }

    [Fact]
    public void GivenEmptyTable_WhenRendering_ThenThereAreNoLines()
    {
        Assert.Empty(Lines("""{"type":"table","content":[]}""", 40));
    }
}
