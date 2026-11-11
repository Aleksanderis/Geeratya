using Geeratya.Platform.Directories;
using Geeratya.Platform.Terminal;

namespace Geeratya.Platform.Tests.Terminal;

[Trait("Os", "Independent")]
public sealed class TerminalResetTests
{
    [Theory]
    [InlineData(OperatingSystemKind.Linux)]
    [InlineData(OperatingSystemKind.MacOs)]
    public void GivenUnixTerminal_WhenAskingToWrite_ThenYesWithoutAskingTheConsole(OperatingSystemKind system)
    {
        var probe = new FakeProbe(false);

        Assert.True(new TerminalReset(system, probe).CanWriteSequences());
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public void GivenWindowsConsoleWithoutVirtualTerminal_WhenAskingToWrite_ThenNo()
    {
        var reset = new TerminalReset(OperatingSystemKind.Windows, new FakeProbe(false));

        Assert.False(reset.CanWriteSequences());
    }

    [Fact]
    public void GivenWindowsConsoleWithVirtualTerminal_WhenAskingToWrite_ThenYes()
    {
        var reset = new TerminalReset(OperatingSystemKind.Windows, new FakeProbe(true));

        Assert.True(reset.CanWriteSequences());
    }

    private sealed class FakeProbe : IConsoleModeProbe
    {
        private readonly bool _enabled;

        public FakeProbe(bool enabled)
        {
            _enabled = enabled;
        }

        public int Calls { get; private set; }

        public bool OutputSequencesEnabled()
        {
            Calls++;

            return _enabled;
        }
    }
}
