using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering.Themes;

public sealed class ThemeCatalogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-themes-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void GivenNoThemesFolder_WhenListing_ThenOnlyBuiltInsAreOffered()
    {
        var catalog = new ThemeCatalog(_directory);

        Assert.Equal(BuiltInThemes.Names, catalog.Names);
        Assert.Empty(catalog.Problems);
    }

    [Fact]
    public void GivenNullFolder_WhenListing_ThenOnlyBuiltInsAreOffered()
    {
        Assert.Equal(BuiltInThemes.Names, new ThemeCatalog(null).Names);
    }

    [Fact]
    public void GivenValidUserFile_WhenListing_ThenItIsOfferedAndFound()
    {
        Write("my-theme.json", UserFile("#ff0000"));

        var catalog = new ThemeCatalog(_directory);

        Assert.Contains("my-theme", catalog.Names);
        Assert.Equal(Rgb.FromHex(0xFF0000), catalog.Find("My-Theme")!.Theme!.Colors[ThemeColor.Accent]);
    }

    [Fact]
    public void GivenUserFileWithABuiltInName_WhenFinding_ThenTheUserFileWinsAndTheNameIsListedOnce()
    {
        Write("nord.json", UserFile("#ff0000"));

        var catalog = new ThemeCatalog(_directory);

        Assert.Equal(Rgb.FromHex(0xFF0000), catalog.Find("nord")!.Theme!.Colors[ThemeColor.Accent]);
        Assert.Single(catalog.Names, name => name == "nord");
    }

    [Fact]
    public void GivenInvalidUserFile_WhenListing_ThenItIsNotOfferedAndTheReasonsAreReported()
    {
        Write("broken.json", """{ "variant": "dark", "colors": { "accent": "red" } }""");
        Write("good.json", UserFile("#00ff00"));

        var catalog = new ThemeCatalog(_directory);

        Assert.DoesNotContain("broken", catalog.Names);
        Assert.Contains("good", catalog.Names);
        Assert.Null(catalog.Find("broken"));
        Assert.Contains(catalog.Problems, problem => problem.StartsWith("broken.json: colors.accent", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("dark.json")]
    [InlineData("light.json")]
    [InlineData("auto.json")]
    public void GivenUserFileWithAReservedName_WhenListing_ThenItIsRefusedAndTheAliasStillWorks(string file)
    {
        Write(file, UserFile("#ff0000"));

        var catalog = new ThemeCatalog(_directory);

        Assert.Contains(catalog.Problems, problem => problem.Contains("reserved", StringComparison.Ordinal));
        Assert.Equal("catppuccin-mocha", catalog.Find("dark")!.Theme!.Name);
    }

    [Fact]
    public void GivenNonJsonFile_WhenListing_ThenItIsIgnored()
    {
        Write("notes.txt", "hello");

        var catalog = new ThemeCatalog(_directory);

        Assert.Empty(catalog.Problems);
        Assert.Equal(BuiltInThemes.Names, catalog.Names);
    }

    [Fact]
    public void GivenAliasAndOddCase_WhenFinding_ThenTheBuiltInComesBack()
    {
        var catalog = new ThemeCatalog(_directory);

        Assert.Equal("catppuccin-latte", catalog.Find(" LIGHT ")!.Theme!.Name);
        Assert.Null(catalog.Find("missing"));
    }

    private static string UserFile(string accent)
    {
        return $$"""
            {
              "variant": "dark",
              "colors": {
                "accent": "{{accent}}", "good": "#a6e3a1", "bad": "#f38ba8", "remote": "#cba6f7",
                "warn": "#f9e2af", "info": "#89dceb", "muted": "#979db5", "border": "#6c7086",
                "barBackground": "#181825", "selection": "#45475a",
                "baseForeground": "#cdd6f4", "baseBackground": "#1e1e2e"
              }
            }
            """;
    }

    private void Write(string name, string text)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, name), text);
    }
}
