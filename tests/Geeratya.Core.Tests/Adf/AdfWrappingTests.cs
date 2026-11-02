using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using static Geeratya.Core.Tests.Adf.AdfTestSupport;

namespace Geeratya.Core.Tests.Adf;

public sealed class AdfWrappingTests
{
    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(80)]
    [InlineData(120)]
    public void GivenRecordedDescriptions_WhenRendering_ThenNoLineIsWiderThanTheWidth(int width)
    {
        foreach (var fixture in new[] { RichDescription, MediaDescription })
        {
            var rendering = AdfRenderer.Render(FixtureDescription(fixture), width);

            Assert.All(rendering.Lines, line => Assert.True(DisplayWidth.Of(line.PlainText) <= width, line.PlainText));
        }
    }

    [Fact]
    public void GivenWordLongerThanTheWidth_WhenRendering_ThenItIsHardWrapped()
    {
        Assert.Equal(["abcd", "efgh", "ij"], Lines(Paragraph("abcdefghij"), 4));
    }

    [Fact]
    public void GivenRunsOfSpaces_WhenRendering_ThenTheyCollapseAndLeadingSpacesAreDropped()
    {
        Assert.Equal(["a b", "c"], Lines(Paragraph("  a    b   c"), 3));
    }

    [Fact]
    public void GivenWidthOfOne_WhenRendering_ThenEveryCharacterGetsALineAndItTerminates()
    {
        var rendering = AdfRenderer.Render(Document(Paragraph("ab cd")), 1);

        Assert.Equal(["a", "b", "c", "d"], PlainLines(rendering));
    }

    [Fact]
    public void GivenWidthOfZeroOrLess_WhenRendering_ThenItIsTreatedAsOne()
    {
        Assert.Equal(1, AdfRenderer.Render(Document(Paragraph("ab")), 0).Width);
        Assert.Equal(["a", "b"], Lines(Paragraph("ab"), -5));
    }

    [Fact]
    public void GivenNestedStructureAtWidthOne_WhenRendering_ThenItTerminatesWithoutPrefixes()
    {
        const string json = """
            {"type":"blockquote","content":[{"type":"bulletList","content":[{"type":"listItem","content":[
              {"type":"paragraph","content":[{"type":"text","text":"ab"}]}]}]}]}
            """;

        Assert.Equal(["a", "b"], Lines(json, 1));
    }

    [Fact]
    public void GivenCjkText_WhenRendering_ThenWideCharactersAreNeverSplit()
    {
        var rendering = AdfRenderer.Render(Document(Paragraph("日本語のテキスト")), 5);

        Assert.Equal(["日本", "語の", "テキ", "スト"], PlainLines(rendering));
        Assert.All(rendering.Lines, line => Assert.True(DisplayWidth.Of(line.PlainText) <= 5));
    }

    [Fact]
    public void GivenWideCharacterAtWidthOne_WhenRendering_ThenItGetsItsOwnLine()
    {
        Assert.Equal(["日", "本"], Lines(Paragraph("日本"), 1));
    }

    [Fact]
    public void GivenEmojiAndAccents_WhenRendering_ThenWidthCountsEmojiAsTwoAndAccentsAsOne()
    {
        var rendering = AdfRenderer.Render(Document(Paragraph("żółć 🚀 żółć")), 7);

        Assert.Equal(["żółć 🚀", "żółć"], PlainLines(rendering));
    }

    [Fact]
    public void GivenCombiningMark_WhenWrapping_ThenItStaysWithItsBase()
    {
        var text = "ééé";

        Assert.Equal(["éé", "é"], Lines(Paragraph(text), 2));
    }

    [Fact]
    public void GivenKeycapEmoji_WhenWrapping_ThenItIsOneCellOfTwoColumnsAndNeverSplit()
    {
        const string keycap = "1\uFE0F\u20E3";

        Assert.Equal(["a" + keycap, "b"], Lines(Paragraph($"a{keycap}b"), 3));
        Assert.Equal(["x", keycap], Lines(Paragraph($"x{keycap}"), 2));
        Assert.Equal([keycap], Lines(Paragraph(keycap), 2));
    }

    [Fact]
    public void GivenKeycapInTable_WhenRendering_ThenRowsAlignByTwoColumns()
    {
        var json = $$"""
            {"type":"table","content":[
              {"type":"tableRow","content":[
                {"type":"tableCell","content":[{{Paragraph("1\uFE0F\u20E3")}}]}]},
              {"type":"tableRow","content":[
                {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"abc"}]}]}]}]}
            """;

        Assert.Equal(["| 1\uFE0F\u20E3  |", "| abc |"], Lines(json, 80));
    }

    [Fact]
    public void GivenAsciiBeforeJoiner_WhenWrapping_ThenAsciiJoinsItsCluster()
    {
        Assert.Equal(["a\u200D"], Lines(Paragraph("a\u200D"), 1));
    }

    [Fact]
    public void GivenCodeBlockWithTabAndWideText_WhenRendering_ThenTabsBecomeSpacesAndWidthHolds()
    {
        const string json = """{"type":"codeBlock","content":[{"type":"text","text":"\t日本語"}]}""";

        var rendering = AdfRenderer.Render(Document(json), 6);

        Assert.Equal(["```", "    日", "本語", "```"], PlainLines(rendering));
    }
}
