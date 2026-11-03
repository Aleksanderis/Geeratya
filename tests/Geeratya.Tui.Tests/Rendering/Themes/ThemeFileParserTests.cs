using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering.Themes;

[Trait("Os", "Independent")]
public class ThemeFileParserTests
{
    private const string Colors = """
        "colors": {
          "accent": "#89b4fa", "good": "#a6e3a1", "bad": "#f38ba8", "remote": "#cba6f7",
          "warn": "#f9e2af", "info": "#89dceb", "muted": "#979db5", "border": "#6c7086",
          "barBackground": "#181825", "selection": "#45475a",
          "baseForeground": "#cdd6f4", "baseBackground": "#1e1e2e"
        }
        """;

    [Fact]
    public void GivenNoOverlayBackground_WhenParsing_ThenTheBoxIsThePageColour()
    {
        var result = ThemeFileParser.Parse("mine", $$"""{ "variant": "dark", {{Colors}} }""");

        Assert.Equal(result.Theme!.PageBackground, result.Theme.Surface);
    }

    [Fact]
    public void GivenAnOverlayBackground_WhenParsing_ThenTheBoxUsesIt()
    {
        var text = $$"""{ "variant": "dark", {{WithOverlay("#102030")}} }""";

        var result = ThemeFileParser.Parse("mine", text);

        Assert.Empty(result.Errors);
        Assert.Equal(Rgb.FromHex(0x102030), result.Theme!.Surface);
    }

    [Fact]
    public void GivenABadOverlayBackground_WhenParsing_ThenTheFileIsRefusedWithOneReason()
    {
        var text = $$"""{ "variant": "dark", {{WithOverlay("blue")}} }""";

        var result = ThemeFileParser.Parse("mine", text);

        Assert.Null(result.Theme);
        Assert.Single(result.Errors);
        Assert.Contains("overlayBackground", result.Errors[0]);
    }

    [Fact]
    public void GivenValidFileWithCommentsAndTrailingCommas_WhenParsing_ThenThemeComesBack()
    {
        var text = $$"""
            // A comment
            { "variant": "Light", {{Colors}}, }
            """;

        var result = ThemeFileParser.Parse("mine", text);

        Assert.Empty(result.Errors);
        Assert.Equal("mine", result.Theme!.Name);
        Assert.Equal(ThemeVariant.Light, result.Theme.Variant);
        Assert.Equal(Rgb.FromHex(0x89B4FA), result.Theme.Colors[ThemeColor.Accent]);
    }

    [Fact]
    public void GivenMissingVariant_WhenParsing_ThenTheFileIsRefused()
    {
        var result = ThemeFileParser.Parse("mine", $"{{ {Colors} }}");

        Assert.Null(result.Theme);
        Assert.Contains(result.Errors, error => error.StartsWith("variant must be", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenMissingAndMalformedColours_WhenParsing_ThenEveryProblemIsReported()
    {
        const string text = """{ "variant": "dark", "colors": { "accent": "blue", "good": "#a6e3a1" } }""";

        var result = ThemeFileParser.Parse("mine", text);

        Assert.Null(result.Theme);
        Assert.Contains("colors.accent: 'blue' is not a colour like #RRGGBB", result.Errors);
        Assert.Contains("colors.bad is missing", result.Errors);
        Assert.Equal(11, result.Errors.Count);
    }

    [Fact]
    public void GivenUnknownVersion_WhenParsing_ThenTheFileIsRefused()
    {
        var result = ThemeFileParser.Parse("mine", $$"""{ "version": 2, "variant": "dark", {{Colors}} }""");

        Assert.Null(result.Theme);
        Assert.Contains("version must be 1", result.Errors);
    }

    [Fact]
    public void GivenUnknownKeys_WhenParsing_ThenTheyAreIgnored()
    {
        var result = ThemeFileParser.Parse("mine", $$"""{ "variant": "dark", "extra": 1, {{Colors}} }""");

        Assert.NotNull(result.Theme);
    }

    [Fact]
    public void GivenBadAnsiColour_WhenParsing_ThenTheFileIsRefused()
    {
        var result = ThemeFileParser.Parse(
            "mine",
            $$"""{ "variant": "dark", "ansi": { "accent": "orange", "good": "BrightGreen" }, {{Colors}} }""");

        Assert.Null(result.Theme);
        Assert.Contains("ansi.accent: 'orange' is not one of the 16 colours", result.Errors);
    }

    [Fact]
    public void GivenAnsiNumberInsteadOfName_WhenParsing_ThenTheFileIsRefused()
    {
        var result = ThemeFileParser.Parse(
            "mine",
            $$"""{ "variant": "dark", "ansi": { "accent": "3" }, {{Colors}} }""");

        Assert.Null(result.Theme);
    }

    [Fact]
    public void GivenAnsiNameInAnyCase_WhenParsing_ThenItIsUsed()
    {
        var result = ThemeFileParser.Parse(
            "mine",
            $$"""{ "variant": "dark", "ansi": { "good": "brightgreen" }, {{Colors}} }""");

        Assert.Equal(Ansi16Color.BrightGreen, result.Theme!.Ansi[ThemeColor.Good]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "variant": "dark", "colors": { "accent": 5 } }""")]
    public void GivenGarbage_WhenParsing_ThenTheFileIsRefusedWithAReason(string text)
    {
        var result = ThemeFileParser.Parse("mine", text);

        Assert.Null(result.Theme);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void GivenLowContrastColours_WhenParsing_ThenTheUserThemeIsStillAccepted()
    {
        var text = $$"""{ "variant": "dark", {{Colors.Replace("#cdd6f4", "#1f1f2f")}} }""";

        Assert.NotNull(ThemeFileParser.Parse("mine", text).Theme);
    }

    private static string WithOverlay(string colour)
    {
        return Colors.Replace("\"colors\": {", $"\"colors\": {{ \"overlayBackground\": \"{colour}\",");
    }
}
