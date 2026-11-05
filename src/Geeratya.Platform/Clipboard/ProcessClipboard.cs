using System.Text;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Clipboard;

/// <summary>
/// Copies through the clipboard program of the operating system, trying each candidate in order until one works.
/// Programs get their arguments as a list and the text on standard input (Windows: inside a base64 argument), so
/// no shell ever parses it. The tool is started detached from its output, because <c>wl-copy</c> and <c>xclip</c>
/// leave a child running that keeps the clipboard alive.
/// </summary>
public sealed class ProcessClipboard : IClipboard
{
    /// <summary>Most characters the Windows command line takes (the limit is 32 767; base64 grows the text by a third).</summary>
    public const int WindowsMaxCharacters = 8000;

    private const string WaylandVariable = "WAYLAND_DISPLAY";
    private const string DisplayVariable = "DISPLAY";
    private const string NoToolMessage = "no clipboard tool found (install wl-clipboard, xclip or xsel)";
    private const string TooLargeMessage = "the text is too large for the Windows clipboard command";

    private readonly OperatingSystemKind _operatingSystem;
    private readonly EnvironmentReader _environment;
    private readonly IProcessRunner _processRunner;

    public ProcessClipboard(
        OperatingSystemKind operatingSystem,
        EnvironmentReader environment,
        IProcessRunner processRunner)
    {
        _operatingSystem = operatingSystem;
        _environment = environment;
        _processRunner = processRunner;
    }

    /// <summary>
    /// The programs to try, in order, for the system; empty when the system has none. Windows passes the text in the
    /// command, so a text past <see cref="WindowsMaxCharacters"/> has no candidate.
    /// </summary>
    public static IReadOnlyList<ClipboardCommand> CandidatesFor(
        OperatingSystemKind operatingSystem,
        EnvironmentReader environment,
        string text)
    {
        switch (operatingSystem)
        {
            case OperatingSystemKind.MacOs:
                return [new ClipboardCommand("pbcopy", [])];
            case OperatingSystemKind.Windows:
                return text.Length > WindowsMaxCharacters ? [] : [WindowsCommand(text)];
            case OperatingSystemKind.Linux:
                return LinuxCommands(environment);
            default:
                return [];
        }
    }

    public async Task<ClipboardResult> CopyAsync(string text, CancellationToken cancellationToken)
    {
        if (_operatingSystem == OperatingSystemKind.Windows && text.Length > WindowsMaxCharacters)
        {
            return ClipboardResult.Failure(TooLargeMessage);
        }

        var candidates = CandidatesFor(_operatingSystem, _environment, text);
        var failure = NoToolMessage;

        foreach (var command in candidates)
        {
            var attempt = await TryAsync(command, text, cancellationToken);

            if (attempt.Copied)
            {
                return attempt;
            }

            if (attempt.Message.Length > 0)
            {
                failure = attempt.Message;
            }
        }

        return ClipboardResult.Failure(failure);
    }

    private static List<ClipboardCommand> LinuxCommands(EnvironmentReader environment)
    {
        var commands = new List<ClipboardCommand>();

        if (environment.Get(WaylandVariable) is not null)
        {
            commands.Add(new ClipboardCommand("wl-copy", []));
        }

        if (environment.Get(DisplayVariable) is not null)
        {
            commands.Add(new ClipboardCommand("xclip", ["-selection", "clipboard"]));
            commands.Add(new ClipboardCommand("xsel", ["--clipboard", "--input"]));
        }

        return commands;
    }

    private static ClipboardCommand WindowsCommand(string text)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        var script = $"Set-Clipboard -Value ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{encoded}')))";

        return new ClipboardCommand("powershell", ["-NoProfile", "-NonInteractive", "-Command", script]);
    }

    // An empty message means "this tool is not installed": the next candidate is tried without reporting it.
    private async Task<ClipboardResult> TryAsync(
        ClipboardCommand command,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            var standardInput = _operatingSystem == OperatingSystemKind.Windows ? null : text;
            var exitCode = await _processRunner.RunDetachedAsync(
                command.Program,
                command.Arguments,
                standardInput,
                cancellationToken);

            return exitCode == 0
                ? ClipboardResult.Success(command.Program)
                : ClipboardResult.Failure($"{command.Program} failed with exit code {exitCode}");
        }
        catch (ProgramNotFoundException)
        {
            return ClipboardResult.Failure(string.Empty);
        }
        catch (TimeoutException)
        {
            return ClipboardResult.Failure($"{command.Program} did not finish in time");
        }
    }
}
