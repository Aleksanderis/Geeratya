using Geeratya.Platform.Directories;
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
    [InlineData("TERM=xterm", GlyphSet.Unicode)]
    [InlineData("TERM=xterm,LC_ALL=C,LANG=en_US.UTF-8", GlyphSet.Ascii)]
    [InlineData("TERM=xterm,LC_ALL=en_US.UTF-8,LANG=C", GlyphSet.Unicode)]
    [InlineData("TERM=xterm,LC_CTYPE=C.UTF-8,LANG=C", GlyphSet.Unicode)]
    [InlineData("TERM=dumb,LANG=en_US.UTF-8", GlyphSet.Ascii)]
    public void GivenLocaleAndTerm_WhenDetecting_ThenChoosesTheGlyphSet(string variables, GlyphSet expected)
    {
        Assert.Equal(expected, Detect(false, false, variables.Split(',')).Glyphs);
    }

    [Fact]
    public void GivenLinuxVirtualTerminal_WhenDetecting_ThenAsciiAnd16Colors()
    {
        var capabilities = Detect(false, false, "TERM=linux", "COLORTERM=truecolor", "LANG=en_US.UTF-8");

        Assert.Equal(GlyphSet.Ascii, capabilities.Glyphs);
        Assert.Equal(ColorSupport.Basic16, capabilities.Colors);
        Assert.True(capabilities.IsInteractive);
    }

    [Theory]
    [InlineData("TERM=ansi", ColorSupport.Basic16)]
    [InlineData("TERM=cons25", ColorSupport.Basic16)]
    [InlineData("TERM=vt100", ColorSupport.None)]
    [InlineData("TERM=vt220", ColorSupport.None)]
    public void GivenOtherConsoleTerms_WhenDetectingOnLinux_ThenAsciiAndTheirColors(string term, ColorSupport expected)
    {
        var capabilities = Detect(false, false, term);

        Assert.Equal(GlyphSet.Ascii, capabilities.Glyphs);
        Assert.Equal(expected, capabilities.Colors);
    }

    [Theory]
    [InlineData(OperatingSystemKind.Linux)]
    [InlineData(OperatingSystemKind.MacOs)]
    [InlineData(OperatingSystemKind.Windows)]
    public void GivenXtermTrueColor_WhenDetectingOnAnySystem_ThenFullLook(OperatingSystemKind system)
    {
        var capabilities = DetectOn(system, "TERM=xterm-256color", "COLORTERM=truecolor");

        Assert.Equal(GlyphSet.Unicode, capabilities.Glyphs);
        Assert.Equal(ColorSupport.TrueColor, capabilities.Colors);
    }

    [Fact]
    public void GivenWindowsWithoutAnyTerminalVariable_WhenDetecting_ThenClassicConsoleGetsAsciiAnd16Colors()
    {
        var capabilities = DetectOn(OperatingSystemKind.Windows);

        Assert.Equal(GlyphSet.Ascii, capabilities.Glyphs);
        Assert.Equal(ColorSupport.Basic16, capabilities.Colors);
        Assert.True(capabilities.IsInteractive);
    }

    [Fact]
    public void GivenWindowsTerminal_WhenDetecting_ThenUnicodeAndTrueColor()
    {
        var capabilities = DetectOn(OperatingSystemKind.Windows, "WT_SESSION=abc");

        Assert.Equal(GlyphSet.Unicode, capabilities.Glyphs);
        Assert.Equal(ColorSupport.TrueColor, capabilities.Colors);
    }

    [Theory]
    [InlineData("TERM_PROGRAM=vscode")]
    [InlineData("TERM=xterm")]
    [InlineData("ConEmuANSI=ON")]
    [InlineData("ANSICON=80x25")]
    public void GivenWindowsWithAModernTerminalMarker_WhenDetecting_ThenNotClassic(string marker)
    {
        Assert.Equal(GlyphSet.Unicode, DetectOn(OperatingSystemKind.Windows, marker).Glyphs);
    }

    [Fact]
    public void GivenWindowsTerminalAndNoColor_WhenDetecting_ThenNoColor()
    {
        Assert.Equal(ColorSupport.None, DetectOn(OperatingSystemKind.Windows, "WT_SESSION=abc", "NO_COLOR=1").Colors);
    }

    [Fact]
    public void GivenWindowsTerminalAndNonUtf8Locale_WhenDetecting_ThenAscii()
    {
        Assert.Equal(GlyphSet.Ascii, DetectOn(OperatingSystemKind.Windows, "WT_SESSION=abc", "LC_ALL=C").Glyphs);
    }

    [Fact]
    public void GivenWindowsClassicConsoleWithRedirectedOutput_WhenDetecting_ThenNoColorAndNotInteractive()
    {
        var capabilities = ConsoleCapabilities.Detect(
            new EnvironmentReader(_ => null),
            false,
            true,
            OperatingSystemKind.Windows);

        Assert.Equal(ColorSupport.None, capabilities.Colors);
        Assert.False(capabilities.IsInteractive);
    }

    [Fact]
    public void GivenMacTerminalWithoutColorterm_WhenDetecting_ThenUnicodeAnd16Colors()
    {
        var capabilities = DetectOn(OperatingSystemKind.MacOs, "TERM=xterm-256color", "TERM_PROGRAM=Apple_Terminal");

        Assert.Equal(GlyphSet.Unicode, capabilities.Glyphs);
        Assert.Equal(ColorSupport.Basic16, capabilities.Colors);
    }

    private static ConsoleCapabilities DetectOn(OperatingSystemKind system, params string[] variables)
    {
        var dictionary = variables
            .Select(variable => variable.Split('='))
            .ToDictionary(pair => pair[0], pair => pair[1]);
        var environment = new EnvironmentReader(name => dictionary.GetValueOrDefault(name));

        return ConsoleCapabilities.Detect(environment, false, false, system);
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
