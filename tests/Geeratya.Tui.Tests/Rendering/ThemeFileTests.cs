using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

public sealed class ThemeFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-themes-{Guid.NewGuid():N}");

    public ThemeFileTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void GivenOneRoleChanged_WhenParsing_ThenOtherRolesComeFromTheBase()
    {
        var result = ThemeFile.Parse(
            "mine",
            """{ "base": "light", "roles": { "link": { "foreground": "#0000AA", "style": ["bold"] } } }""");

        var theme = Assert.IsType<Theme>(result.Theme);
        var link = theme.Get(PaletteRole.Link);

        Assert.Equal(new Rgb(0, 0, 0xAA), link.TrueForeground);
        Assert.Equal(RoleStyle.Bold, link.NoColorStyle);
        Assert.Equal(Themes.Light.Get(PaletteRole.Link).TrueBackground, link.TrueBackground);
        Assert.Equal(Themes.Light.Get(PaletteRole.Body), theme.Get(PaletteRole.Body));
        Assert.Equal("mine", theme.Name);
    }

    [Fact]
    public void GivenEmptyObject_WhenParsing_ThenItIsTheDarkTheme()
    {
        var result = ThemeFile.Parse("copy", "{}");

        Assert.Equal(Themes.Dark.Entries.Count, result.Theme!.Entries.Count);
        Assert.Equal(Themes.Dark.Get(PaletteRole.Error), result.Theme.Get(PaletteRole.Error));
    }

    [Theory]
    [InlineData("""{ "roles": { "nothing": {} } }""", "unknown role 'nothing'")]
    [InlineData("""{ "roles": { "body": { "foreground": "red" } } }""", "not a colour like #RRGGBB")]
    [InlineData("""{ "roles": { "body": { "foreground16": "purple" } } }""", "not one of the 16 colours")]
    [InlineData("""{ "roles": { "body": { "style": ["sparkly"] } } }""", "not bold, underline")]
    [InlineData("""{ "base": "neon" }""", "unknown base theme")]
    [InlineData("{ not json", "not valid JSON")]
    [InlineData("null", "empty")]
    [InlineData("""{ "roles": { "link": null } }""", "role 'link' is empty")]
    [InlineData("""{ "base": 5 }""", "not valid JSON")]
    [InlineData("""{ "roles": { "body,muted": {} } }""", "unknown role")]
    [InlineData("""{ "roles": { "body": { "foreground16": "3" } } }""", "not one of the 16 colours")]
    [InlineData("""{ "roles": { "selected": { "style": ["bold"] } } }""", "must include reverse")]
    [InlineData("""{ "roles": { "status": { "style": [] } } }""", "must include reverse")]
    public void GivenAProblem_WhenParsing_ThenTheThemeIsRefusedWithTheReason(string json, string reason)
    {
        var result = ThemeFile.Parse("bad", json);

        Assert.Null(result.Theme);
        Assert.Contains(result.Errors, error => error.Contains(reason));
    }

    [Fact]
    public void GivenLowContrast_WhenParsing_ThenTheThemeIsRefusedNamingTheRole()
    {
        var result = ThemeFile.Parse(
            "dim",
            """{ "roles": { "body": { "foreground": "#1F232B" }, "divider": { "foreground": "#1F232B" } } }""");

        Assert.Null(result.Theme);
        Assert.Contains(result.Errors, error => error.StartsWith("body: contrast") && error.Contains("24-bit"));
        Assert.Contains(result.Errors, error => error.StartsWith("divider: contrast"));
    }

    [Fact]
    public void GivenTwoProblems_WhenParsing_ThenBothAreReported()
    {
        var result = ThemeFile.Parse("bad", """{ "roles": { "x": {}, "body": { "foreground": "no" } } }""");

        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void GivenThemeFiles_WhenListingAndLoading_ThenBuiltInsComeFirstAndFilesAreFound()
    {
        File.WriteAllText(Path.Combine(_directory, "zebra.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "amber.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "dark.json"), """{ "roles": { "x": {} } }""");
        File.WriteAllText(Path.Combine(_directory, "bad name.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "notes.txt"), "{}");
        var catalog = new ThemeCatalog(_directory);

        Assert.Equal(["dark", "light", "high-contrast", "ansi16", "amber", "zebra"], catalog.Names());
        Assert.NotNull(catalog.Load("amber").Theme);
        Assert.Same(Themes.Dark, catalog.Load("DARK").Theme);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("../escape")]
    [InlineData("")]
    public void GivenAnUnknownOrUnsafeName_WhenLoading_ThenThereIsNoThemeAndNothingThrows(string name)
    {
        var result = new ThemeCatalog(_directory).Load(name);

        Assert.Null(result.Theme);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void GivenNoThemesFolder_WhenListing_ThenOnlyBuiltInsShow()
    {
        var catalog = new ThemeCatalog(Path.Combine(_directory, "nowhere"));

        Assert.Equal(Themes.All.Count, catalog.Names().Count);
        Assert.Equal(Themes.All.Count, new ThemeCatalog(null).Names().Count);
    }

    [Fact]
    public void GivenAUserTheme_WhenResolving_ThenItsColoursReachTheStyle()
    {
        var theme = ThemeFile.Parse("mine", """{ "roles": { "body": { "foreground": "#FFFFFF" } } }""").Theme!;

        var style = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body);

        Assert.Equal(new Rgb(255, 255, 255), style.Foreground);
    }
}
