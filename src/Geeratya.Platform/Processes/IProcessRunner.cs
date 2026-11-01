namespace Geeratya.Platform.Processes;

/// <summary>Runs an external program; a seam so tests never start real processes.</summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs the program with the arguments, writes the optional text to its standard input and waits for it.
    /// Throws <see cref="ProgramNotFoundException"/> when the program is not installed.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string program,
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken);
}
