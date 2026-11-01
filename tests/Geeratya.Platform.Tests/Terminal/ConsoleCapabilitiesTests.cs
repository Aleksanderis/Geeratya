using Geeratya.Platform.Terminal;

namespace Geeratya.Platform.Tests.Terminal;

public class ConsoleCapabilitiesTests
{
    [Fact]
    public void Colorterm_truecolor_enables_24_bit()
    {
        var capabilities = Detect(false, false, "TERM=xterm-256color", "COLORTERM=truecolor");

        Assert.Equal(ColorSupport.TrueColor, capabilities.Colors);
        Assert.True(capabilities.IsInteractive);
    }

    [Fact]
    public void Plain_xterm_gets_16_colors()
    {
        Assert.Equal(ColorSupport.Basic16, Detect(false, false, "TERM=xterm").Colors);
    }

    [Fact]
    public void No_color_wins_over_colorterm()
    {
        var capabilities = Detect(false, false, "NO_COLOR=1", "COLORTERM=truecolor");

        Assert.Equal(ColorSupport.None, capabilities.Colors);
    }

    [Fact]
    public void Dumb_terminal_has_no_colors_and_is_not_interactive()
    {
        var capabilities = Detect(false, false, "TERM=dumb");

        Assert.Equal(ColorSupport.None, capabilities.Colors);
        Assert.False(capabilities.IsInteractive);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Redirection_means_not_interactive(bool inputRedirected, bool outputRedirected)
    {
        Assert.False(Detect(inputRedirected, outputRedirected, "TERM=xterm").IsInteractive);
    }

    private static ConsoleCapabilities Detect(bool inputRedirected, bool outputRedirected, params string[] variables)
    {
        var dictionary = variables.Select(variable => variable.Split('=')).ToDictionary(pair => pair[0], pair => pair[1]);
        var environment = new EnvironmentReader(name => dictionary.GetValueOrDefault(name));

        return ConsoleCapabilities.Detect(environment, inputRedirected, outputRedirected);
    }
}
