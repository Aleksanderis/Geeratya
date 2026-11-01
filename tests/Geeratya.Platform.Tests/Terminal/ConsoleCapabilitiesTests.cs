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

    private static ConsoleCapabilities Detect(bool inputRedirected, bool outputRedirected, params string[] variables)
    {
        var dictionary = variables
            .Select(variable => variable.Split('='))
            .ToDictionary(pair => pair[0], pair => pair[1]);
        var environment = new EnvironmentReader(name => dictionary.GetValueOrDefault(name));

        return ConsoleCapabilities.Detect(environment, inputRedirected, outputRedirected);
    }
}
