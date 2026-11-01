namespace Geeratya.Platform.Processes;

/// <summary>The outcome of a finished process: exit code and standard output.</summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput);
