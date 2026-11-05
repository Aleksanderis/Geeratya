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

    /// <summary>
    /// Starts a program that may leave a long-lived child behind (a browser opener) and waits only for the program
    /// itself: its output is not read and, on timeout, nothing is killed. Returns the exit code.
    /// Throws <see cref="ProgramNotFoundException"/> when the program is not installed.
    /// </summary>
    Task<int> RunDetachedAsync(
        string program,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);

    /// <summary>
    /// Like the overload without input, with the text written to the program's standard input first (a clipboard
    /// tool). Throws <see cref="ProgramNotFoundException"/> when the program is not installed.
    /// </summary>
    Task<int> RunDetachedAsync(
        string program,
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken);
}
