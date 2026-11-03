using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering.Themes;

[Trait("Os", "Independent")]
public class BuiltInThemesTests
{
    public static TheoryData<string> Names => [.. BuiltInThemes.Names];

    [Fact]
    public void GivenEmbeddedFiles_WhenComparingWithTheListedNames_ThenTheyAgree()
    {
        Assert.Equal(BuiltInThemes.Names.Order(StringComparer.Ordinal), BuiltInThemes.EmbeddedNames);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void GivenListedName_WhenFinding_ThenTheFileParsesWithoutErrors(string name)
    {
        var result = BuiltInThemes.Find(name);

        Assert.NotNull(result);
        Assert.Empty(result.Errors);
        Assert.Equal(name, result.Theme!.Name);
    }

    [Fact]
    public void GivenTheFamilies_WhenListing_ThenDarkAndLightVariantsAreBothPresent()
    {
        var variants = BuiltInThemes.Names.Select(name => BuiltInThemes.Find(name)!.Theme!.Variant).Distinct();

        Assert.Equal(2, variants.Count());
    }

    [Theory]
    [InlineData("dark", "catppuccin-mocha", ThemeVariant.Dark)]
    [InlineData("light", "catppuccin-latte", ThemeVariant.Light)]
    public void GivenDarkOrLightAlias_WhenFinding_ThenTheCatppuccinThemeComesBack(
        string alias,
        string expected,
        ThemeVariant variant)
    {
        var theme = BuiltInThemes.Find(alias)!.Theme!;

        Assert.Equal(expected, theme.Name);
        Assert.Equal(variant, theme.Variant);
    }

    [Fact]
    public void GivenUnknownName_WhenFinding_ThenNothingComesBack()
    {
        Assert.Null(BuiltInThemes.Find("no-such-theme"));
    }

    [Fact]
    public void GivenTheDefault_WhenReading_ThenItIsCatppuccinMocha()
    {
        Assert.Equal("catppuccin-mocha", BuiltInThemes.Default.Name);
    }
}
