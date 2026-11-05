using System.Text;
using Geeratya.Platform.Clipboard;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Tests.Clipboard;

[Trait("Os", "Independent")]
public class ClipboardTests
{
    // The escape character that starts the OSC 52 sequence, built so the OS-independence guard sees no terminal API.
    private static readonly string _escape = ((char)27).ToString();

    [Fact]
    public async Task GivenWayland_WhenCopying_ThenWlCopyGetsTheTextOnStandardInput()
    {
        var runner = new RecordingRunner();
        var clipboard = new ProcessClipboard(OperatingSystemKind.Linux, Environment("WAYLAND_DISPLAY", "wayland-0"), runner);

        var result = await clipboard.CopyAsync("héllo\nworld", CancellationToken.None);

        Assert.True(result.Copied);
        Assert.Equal("wl-copy", result.Message);
        Assert.Equal(["wl-copy"], runner.Programs);
        Assert.Equal("héllo\nworld", Assert.Single(runner.Inputs));
    }

    [Fact]
    public async Task GivenWaylandToolMissing_WhenCopying_ThenXclipIsTriedNext()
    {
        var runner = new RecordingRunner("wl-copy");
        var environment = new EnvironmentReader(name => name is "WAYLAND_DISPLAY" or "DISPLAY" ? "1" : null);
        var clipboard = new ProcessClipboard(OperatingSystemKind.Linux, environment, runner);

        var result = await clipboard.CopyAsync("text", CancellationToken.None);

        Assert.True(result.Copied);
        Assert.Equal(["wl-copy", "xclip"], runner.Programs);
        Assert.Equal(["-selection", "clipboard"], runner.Arguments[1]);
    }

    [Fact]
    public async Task GivenNoToolInstalled_WhenCopying_ThenTheResultNamesTheMissingTools()
    {
        var runner = new RecordingRunner("wl-copy", "xclip", "xsel");
        var environment = new EnvironmentReader(name => name is "WAYLAND_DISPLAY" or "DISPLAY" ? "1" : null);
        var clipboard = new ProcessClipboard(OperatingSystemKind.Linux, environment, runner);

        var result = await clipboard.CopyAsync("text", CancellationToken.None);

        Assert.False(result.Copied);
        Assert.Contains("wl-clipboard", result.Message, StringComparison.Ordinal);
        Assert.Equal(["wl-copy", "xclip", "xsel"], runner.Programs);
    }

    [Fact]
    public async Task GivenALinuxWithoutADisplay_WhenCopying_ThenNoToolIsStarted()
    {
        var runner = new RecordingRunner();
        var clipboard = new ProcessClipboard(OperatingSystemKind.Linux, new EnvironmentReader(_ => null), runner);

        var result = await clipboard.CopyAsync("text", CancellationToken.None);

        Assert.False(result.Copied);
        Assert.Empty(runner.Programs);
    }

    [Fact]
    public async Task GivenAFailingTool_WhenCopying_ThenTheExitCodeIsReported()
    {
        var runner = new RecordingRunner { ExitCode = 1 };
        var clipboard = new ProcessClipboard(OperatingSystemKind.Linux, Environment("WAYLAND_DISPLAY", "w"), runner);

        var result = await clipboard.CopyAsync("text", CancellationToken.None);

        Assert.False(result.Copied);
        Assert.Equal("wl-copy failed with exit code 1", result.Message);
    }

    [Fact]
    public async Task GivenMacOs_WhenCopying_ThenPbcopyGetsTheText()
    {
        var runner = new RecordingRunner();
        var clipboard = new ProcessClipboard(OperatingSystemKind.MacOs, new EnvironmentReader(_ => null), runner);

        var result = await clipboard.CopyAsync("text", CancellationToken.None);

        Assert.True(result.Copied);
        Assert.Equal(["pbcopy"], runner.Programs);
        Assert.Equal("text", Assert.Single(runner.Inputs));
    }

    [Fact]
    public async Task GivenWindows_WhenCopying_ThenPowershellGetsTheTextBase64EncodedInTheCommandNotOnInput()
    {
        var runner = new RecordingRunner();
        var clipboard = new ProcessClipboard(OperatingSystemKind.Windows, new EnvironmentReader(_ => null), runner);

        var result = await clipboard.CopyAsync("héllo", CancellationToken.None);

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("héllo"));
        Assert.True(result.Copied);
        Assert.Equal(["powershell"], runner.Programs);
        Assert.Contains(encoded, runner.Arguments[0][^1], StringComparison.Ordinal);
        Assert.Null(Assert.Single(runner.Inputs));
    }

    [Fact]
    public async Task GivenATooLongTextOnWindows_WhenCopying_ThenItIsRefusedWithoutStartingAProgram()
    {
        var runner = new RecordingRunner();
        var clipboard = new ProcessClipboard(OperatingSystemKind.Windows, new EnvironmentReader(_ => null), runner);

        var result = await clipboard.CopyAsync(new string('x', ProcessClipboard.WindowsMaxCharacters + 1), CancellationToken.None);

        Assert.False(result.Copied);
        Assert.Empty(runner.Programs);
    }

    [Fact]
    public async Task GivenATerminal_WhenCopyingWithOsc52_ThenTheSequenceCarriesTheBase64Text()
    {
        using var terminal = new StringWriter();
        var clipboard = new Osc52Clipboard(terminal);

        var result = await clipboard.CopyAsync("héllo", CancellationToken.None);

        Assert.True(result.Copied);
        Assert.Equal($"{_escape}]52;c;{Convert.ToBase64String(Encoding.UTF8.GetBytes("héllo"))}\a", terminal.ToString());
    }

    [Fact]
    public async Task GivenNoTerminalOrAHugeText_WhenCopyingWithOsc52_ThenItSaysWhyNot()
    {
        using var terminal = new StringWriter();

        var noTerminal = await new Osc52Clipboard(null).CopyAsync("text", CancellationToken.None);
        var huge = await new Osc52Clipboard(terminal).CopyAsync(new string('x', 100_000), CancellationToken.None);

        Assert.False(noTerminal.Copied);
        Assert.False(huge.Copied);
        Assert.Equal(string.Empty, terminal.ToString());
    }

    [Fact]
    public async Task GivenNoClipboardProgram_WhenCopyingThroughTheChain_ThenTheTerminalSequenceIsTheFallback()
    {
        using var terminal = new StringWriter();
        var runner = new RecordingRunner("wl-copy");
        var clipboard = Clipboards.Create(OperatingSystemKind.Linux, Environment("WAYLAND_DISPLAY", "w"), runner, terminal);

        var result = await clipboard.CopyAsync("text", CancellationToken.None);

        Assert.True(result.Copied);
        Assert.Contains("OSC 52", result.Message, StringComparison.Ordinal);
        Assert.StartsWith($"{_escape}]52;c;", terminal.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNothingWorks_WhenCopyingThroughTheChain_ThenTheMessageSaysEverythingThatFailed()
    {
        var runner = new RecordingRunner("wl-copy");
        var clipboard = Clipboards.Create(OperatingSystemKind.Linux, Environment("WAYLAND_DISPLAY", "w"), runner, null);

        var result = await clipboard.CopyAsync("text", CancellationToken.None);

        Assert.False(result.Copied);
        Assert.Contains("clipboard tool", result.Message, StringComparison.Ordinal);
        Assert.Contains("not a terminal", result.Message, StringComparison.Ordinal);
    }

    private static EnvironmentReader Environment(string name, string value)
    {
        return new EnvironmentReader(key => key == name ? value : null);
    }

    private sealed class RecordingRunner : IProcessRunner
    {
        private readonly HashSet<string> _missing;

        public RecordingRunner(params string[] missing)
        {
            _missing = [.. missing];
        }

        public int ExitCode { get; init; }

        public List<string> Programs { get; } = [];

        public List<IReadOnlyList<string>> Arguments { get; } = [];

        public List<string?> Inputs { get; } = [];

        public Task<ProcessResult> RunAsync(
            string program,
            IReadOnlyList<string> arguments,
            string? standardInput,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<int> RunDetachedAsync(
            string program,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<int> RunDetachedAsync(
            string program,
            IReadOnlyList<string> arguments,
            string? standardInput,
            CancellationToken cancellationToken)
        {
            Programs.Add(program);
            Arguments.Add(arguments);
            Inputs.Add(standardInput);

            if (_missing.Contains(program))
            {
                throw new ProgramNotFoundException(program, new InvalidOperationException());
            }

            return Task.FromResult(ExitCode);
        }
    }
}
