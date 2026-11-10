namespace Geeratya.App.Updates;

/// <summary>The <c>geeratya update</c> command, behind an interface so the runner is tested without a network.</summary>
public interface IUpdateCommand
{
    /// <summary>Runs the command and returns the process exit code; progress and results go to its output.</summary>
    Task<int> RunAsync(bool checkOnly, TextWriter output, CancellationToken cancellationToken);
}
