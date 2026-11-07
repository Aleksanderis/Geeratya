using System.Text;
using Geeratya.Platform.Editors;

namespace Geeratya.Platform.Tests.Editors;

/// <summary>
/// Stands in for the editor process: records the call, reads the file it was given, and writes a replacement text
/// (or nothing) like a user saving in an editor would.
/// </summary>
public sealed class FakeTerminalProgramRunner : ITerminalProgramRunner
{
    private static readonly UTF8Encoding _utf8 = new(false);

    public string? Program { get; private set; }

    public IReadOnlyList<string> Arguments { get; private set; } = [];

    /// <summary>The text the file held when the editor started.</summary>
    public string? SeenText { get; private set; }

    /// <summary>The Unix mode of the file when the editor started, null on Windows.</summary>
    public UnixFileMode? SeenMode { get; private set; }

    /// <summary>The text the "user" saves; null leaves the file as it was.</summary>
    public string? Replacement { get; set; }

    public int ExitCode { get; set; }

    public Exception? Failure { get; set; }

    public string? Path => Arguments.Count == 0 ? null : Arguments[^1];

    public async Task<int> RunAsync(
        string program,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        Program = program;
        Arguments = arguments;

        if (Failure is not null)
        {
            throw Failure;
        }

        var path = arguments[^1];

        SeenText = await File.ReadAllTextAsync(path, _utf8, cancellationToken);
        SeenMode = OperatingSystem.IsWindows() ? null : File.GetUnixFileMode(path);

        if (Replacement is not null)
        {
            await File.WriteAllTextAsync(path, Replacement, _utf8, cancellationToken);
        }

        return ExitCode;
    }
}
