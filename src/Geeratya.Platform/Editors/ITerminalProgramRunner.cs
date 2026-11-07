namespace Geeratya.Platform.Editors;

/// <summary>Runs a program on the real terminal (no redirected streams); a seam so tests start no process.</summary>
public interface ITerminalProgramRunner
{
    /// <summary>
    /// Starts the program with the arguments (never through a shell), waits for it and returns its exit code.
    /// Throws <see cref="Processes.ProgramNotFoundException"/> when the program is not installed.
    /// </summary>
    Task<int> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
