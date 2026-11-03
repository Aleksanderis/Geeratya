using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering.Themes;

public sealed class ThemeSwitcherTests : IDisposable
{
    private const string UserTheme = """
        {
          "variant": "light",
          "colors": {
            "accent": "#0000cc", "good": "#006600", "bad": "#aa0000", "remote": "#660099",
            "warn": "#774400", "info": "#006666", "muted": "#444444", "border": "#777777",
            "barBackground": "#dddddd", "selection": "#bbccee",
            "baseForeground": "#111111", "baseBackground": "#ffffff"
          }
        }
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-switcher-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void GivenSavedBuiltInName_WhenStarting_ThenThatThemeIsUsedWithoutANotice()
    {
        var start = Switcher().Start("nord");

        Assert.Equal("nord", start.Theme.Name);
        Assert.Null(start.Notice);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GivenBlankSavedName_WhenStarting_ThenTheDefaultIsUsed(string? saved)
    {
        var start = Switcher().Start(saved);

        Assert.Equal(BuiltInThemes.DefaultDarkName, start.Theme.Name);
        Assert.Null(start.Notice);
    }

    [Fact]
    public void GivenSavedNameThatIsGone_WhenStarting_ThenTheDefaultIsUsedWithANotice()
    {
        var start = Switcher().Start("my-gone-theme");

        Assert.Equal(BuiltInThemes.DefaultDarkName, start.Theme.Name);
        Assert.Contains("my-gone-theme: not found; using catppuccin-mocha", start.Notice);
    }

    [Fact]
    public void GivenSavedUserFileThatIsInvalid_WhenStarting_ThenTheDefaultIsUsedAndTheReasonsShow()
    {
        Write("broken.json", """{ "variant": "dark" }""");

        var start = Switcher().Start("broken");

        Assert.Equal(BuiltInThemes.DefaultDarkName, start.Theme.Name);
        Assert.Contains("broken.json: colors.accent is missing", start.Notice);
        Assert.Contains("broken: not found; using catppuccin-mocha", start.Notice);
    }

    [Fact]
    public void GivenSavedUserTheme_WhenStarting_ThenItIsUsed()
    {
        Write("mine.json", UserTheme);

        var start = Switcher().Start("mine");

        Assert.Equal("mine", start.Theme.Name);
        Assert.Equal(ThemeVariant.Light, start.Theme.Variant);
        Assert.Null(start.Notice);
    }

    [Fact]
    public void GivenAnExactName_WhenChoosing_ThenThatThemeIsChosen()
    {
        var choice = Switcher().Choose("gruvbox-dark-soft", "nord");

        Assert.Equal("gruvbox-dark-soft", choice.Theme!.Name);
        Assert.Equal("gruvbox-dark-soft", choice.Name);
        Assert.Equal("Theme gruvbox-dark-soft.", choice.Message);
    }

    [Fact]
    public void GivenLightAlias_WhenChoosing_ThenTheSavedNameIsTheCatppuccinFile()
    {
        var choice = Switcher().Choose("light", "nord");

        Assert.Equal("catppuccin-latte", choice.Name);
        Assert.Equal(ThemeVariant.Light, choice.Theme!.Variant);
    }

    [Fact]
    public void GivenNoName_WhenChoosing_ThenFamiliesAreListedAndNothingChanges()
    {
        var choice = Switcher().Choose(string.Empty, "nord");

        Assert.Null(choice.Theme);
        Assert.Equal("nord", choice.Name);
        Assert.Contains("catppuccin", choice.Message);
        Assert.Contains("high-contrast", choice.Message);
        Assert.Contains("gruvbox", choice.Message);
        Assert.DoesNotContain("gruvbox-dark-soft", choice.Message);
        Assert.Contains("now nord", choice.Message);
        Assert.True(choice.Message.Length < 160, choice.Message);
    }

    [Fact]
    public void GivenAWordInsideSeveralNames_WhenChoosing_ThenTheMatchesAreListedAndNothingChanges()
    {
        var choice = Switcher().Choose("tokyo", "nord");

        Assert.Null(choice.Theme);
        Assert.Contains("tokyo-night, tokyo-night-storm, tokyo-night-moon, tokyo-night-day", choice.Message);
    }

    [Fact]
    public void GivenUnknownName_WhenChoosing_ThenItIsNotFoundAndNothingChanges()
    {
        var choice = Switcher().Choose("zzz", "nord");

        Assert.Null(choice.Theme);
        Assert.Contains("zzz not found", choice.Message);
    }

    [Fact]
    public void GivenFileThatIsNotUsed_WhenListing_ThenTheReasonIsMentioned()
    {
        Write("broken.json", """{ "variant": "dark" }""");

        var choice = Switcher().Choose(string.Empty, "nord");

        Assert.Contains("Not used: broken.json: colors.accent is missing", choice.Message);
    }

    [Fact]
    public void GivenManyProblems_WhenStarting_ThenOnlyThreeReasonsShowWithTheRestCounted()
    {
        Write("broken.json", """{ "variant": "dark" }""");

        var start = Switcher().Start("nord");

        Assert.Contains("(+9 more)", start.Notice);
    }

    private ThemeSwitcher Switcher()
    {
        return new ThemeSwitcher(new ThemeCatalog(_directory));
    }

    private void Write(string name, string text)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, name), text);
    }
}
