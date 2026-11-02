using Geeratya.Platform.Terminal;

namespace Geeratya.Platform.Tests.Terminal;

public class ConsoleCapabilitiesTests
{
    [Fact]
    public void GivenColortermTruecolor_WhenDetecting_ThenEnables24Bit()
    {
        var capabilities = Detect(false, false, "TERM=xterm-256color", "COLORTERM=truecolor");

        Assert.Equal(ColorSupport.TrueColor, capabilities.Colors);
        Assert.True(capabilities.IsInteractive);
    }

    [Fact]
    public void GivenPlainXterm_WhenDetecting_ThenGets16Colors()
    {
        Assert.Equal(ColorSupport.Basic16, Detect(false, false, "TERM=xterm").Colors);
    }

    [Fact]
    public void GivenNoColorAndColorterm_WhenDetecting_ThenNoColorWins()
    {
        var capabilities = Detect(false, false, "NO_COLOR=1", "COLORTERM=truecolor");

        Assert.Equal(ColorSupport.None, capabilities.Colors);
    }

    [Fact]
    public void GivenDumbTerminal_WhenDetecting_ThenNoColorsAndNotInteractive()
    {
        var capabilities = Detect(false, false, "TERM=dumb");

        Assert.Equal(ColorSupport.None, capabilities.Colors);
        Assert.False(capabilities.IsInteractive);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GivenRedirection_WhenDetecting_ThenNotInteractive(bool inputRedirected, bool outputRedirected)
    {
        Assert.False(Detect(inputRedirected, outputRedirected, "TERM=xterm").IsInteractive);
    }

    [Theory]
    [InlineData("TERM=xterm,LANG=en_US.UTF-8", GlyphSet.Unicode)]
    [InlineData("TERM=xterm,LANG=en_US.utf8", GlyphSet.Unicode)]
    [InlineData("TERM=xterm,LANG=C", GlyphSet.Ascii)]
    [InlineData("TERM=xterm,LANG=en_US.ISO-8859-1", GlyphSet.Ascii)]
    [InlineData("TERM=xterm", GlyphSet.Ascii)]
    [InlineData("TERM=xterm,LC_ALL=C,LANG=en_US.UTF-8", GlyphSet.Ascii)]
    [InlineData("TERM=xterm,LC_ALL=en_US.UTF-8,LANG=C", GlyphSet.Unicode)]
    [InlineData("TERM=xterm,LC_CTYPE=C.UTF-8,LANG=C", GlyphSet.Unicode)]
    [InlineData("TERM=dumb,LANG=en_US.UTF-8", GlyphSet.Ascii)]
    public void GivenLocaleAndTerm_WhenDetecting_ThenChoosesTheGlyphSet(string variables, GlyphSet expected)
    {
        Assert.Equal(expected, Detect(false, false, variables.Split(',')).Glyphs);
    }

    [Fact]
    public void GivenWindowsWithoutLocaleVariables_WhenDetecting_ThenUnicode()
    {
        var environment = new EnvironmentReader(_ => null);

        Assert.Equal(GlyphSet.Unicode, ConsoleCapabilities.Detect(environment, false, false, true).Glyphs);
    }

    private static ConsoleCapabilities Detect(bool inputRedirected, bool outputRedirected, params string[] variables)
    {
        var dictionary = variables
            .Select(variable => variable.Split('='))
            .ToDictionary(pair => pair[0], pair => pair[1]);
        var environment = new EnvironmentReader(name => dictionary.GetValueOrDefault(name));

        return ConsoleCapabilities.Detect(environment, inputRedirected, outputRedirected);
    }
}
