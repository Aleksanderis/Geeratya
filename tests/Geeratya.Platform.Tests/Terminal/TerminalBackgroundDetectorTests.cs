using Geeratya.Platform.Directories;
using Geeratya.Platform.Terminal;

namespace Geeratya.Platform.Tests.Terminal;

public class TerminalBackgroundDetectorTests
{
    private const string LightAnswer = "\u001b]11;rgb:ffff/ffff/ffff\\";
    private const string DarkAnswer = "\u001b]11;rgb:1e1e/1e1e/2e2e\\";

    [Fact]
    public void GivenALightAnswer_WhenDetecting_ThenItIsLight()
    {
        var query = new FakeQuery(LightAnswer);

        Assert.True(Detector(query, ["TERM=xterm"]).IsLight(allowQuery: true));
        Assert.Equal(TerminalBackgroundDetector.DefaultTimeout, query.Timeout);
    }

    [Fact]
    public void GivenADarkAnswerAndLightColorFgBg_WhenDetecting_ThenTheAnswerWins()
    {
        var detector = Detector(new FakeQuery(DarkAnswer), ["TERM=xterm", "COLORFGBG=0;15"]);

        Assert.False(detector.IsLight(allowQuery: true));
    }

    [Fact]
    public void GivenNoAnswerAndColorFgBg_WhenDetecting_ThenColorFgBgDecides()
    {
        var detector = Detector(new FakeQuery(null), ["TERM=xterm", "COLORFGBG=0;15"]);

        Assert.True(detector.IsLight(allowQuery: true));
    }

    [Fact]
    public void GivenNeitherAnswerNorColorFgBg_WhenDetecting_ThenItIsUnknown()
    {
        Assert.Null(Detector(new FakeQuery(null), ["TERM=xterm"]).IsLight(allowQuery: true));
    }

    [Fact]
    public void GivenAnUnparsableAnswer_WhenDetecting_ThenColorFgBgDecides()
    {
        var detector = Detector(new FakeQuery("garbage"), ["TERM=xterm", "COLORFGBG=15;0"]);

        Assert.False(detector.IsLight(allowQuery: true));
    }

    [Theory]
    [InlineData("TERM=dumb", "")]
    [InlineData("TERM=linux", "")]
    [InlineData("TERM=xterm", "NO_COLOR=1")]
    public void GivenASkippingEnvironment_WhenDetecting_ThenTheTerminalIsNotAsked(string term, string other)
    {
        var query = new FakeQuery(LightAnswer);

        Assert.Null(Detector(query, [term, other]).IsLight(allowQuery: true));
        Assert.Equal(0, query.Calls);
    }

    [Fact]
    public void GivenTermUnset_WhenDetecting_ThenTheTerminalIsNotAsked()
    {
        var query = new FakeQuery(LightAnswer);

        Assert.Null(Detector(query, []).IsLight(allowQuery: true));
        Assert.Equal(0, query.Calls);
    }

    [Fact]
    public void GivenNotInteractive_WhenDetecting_ThenTheTerminalIsNotAsked()
    {
        var query = new FakeQuery(LightAnswer);
        var detector = Detector(query, ["TERM=xterm"], interactive: false);

        Assert.Null(detector.IsLight(allowQuery: true));
        Assert.Equal(0, query.Calls);
    }

    [Fact]
    public void GivenWindows_WhenDetecting_ThenTheTerminalIsNotAskedAndColorFgBgStillCounts()
    {
        var query = new FakeQuery(LightAnswer);
        var detector = Detector(query, ["TERM=xterm", "COLORFGBG=15;0"], system: OperatingSystemKind.Windows);

        Assert.False(detector.IsLight(allowQuery: true));
        Assert.Equal(0, query.Calls);
    }

    [Theory]
    [InlineData(OperatingSystemKind.Linux)]
    [InlineData(OperatingSystemKind.MacOs)]
    public void GivenUnixLikeSystems_WhenDetecting_ThenTheTerminalIsAsked(OperatingSystemKind system)
    {
        var query = new FakeQuery(LightAnswer);

        Assert.True(Detector(query, ["TERM=xterm"], system: system).IsLight(allowQuery: true));
        Assert.Equal(1, query.Calls);
    }

    [Fact]
    public void GivenQueryNotAllowed_WhenDetecting_ThenOnlyColorFgBgIsUsed()
    {
        var query = new FakeQuery(DarkAnswer);
        var detector = Detector(query, ["TERM=xterm", "COLORFGBG=0;15"]);

        Assert.True(detector.IsLight(allowQuery: false));
        Assert.Equal(0, query.Calls);
    }

    private static TerminalBackgroundDetector Detector(
        FakeQuery query,
        string[] variables,
        bool interactive = true,
        OperatingSystemKind system = OperatingSystemKind.Linux)
    {
        var map = variables
            .Where(variable => variable.Length > 0)
            .Select(variable => variable.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1]);
        var environment = new EnvironmentReader(name => map.GetValueOrDefault(name));

        return new TerminalBackgroundDetector(environment, interactive, system, query);
    }

    private sealed class FakeQuery(string? answer) : ITerminalBackgroundQuery
    {
        public int Calls { get; private set; }

        public TimeSpan? Timeout { get; private set; }

        public string? Ask(TimeSpan timeout)
        {
            Calls++;
            Timeout = timeout;

            return answer;
        }
    }
}
