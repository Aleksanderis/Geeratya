using Geeratya.App.Updates;
using Geeratya.Core;
using Geeratya.Platform.Directories;

namespace Geeratya.Tui;

public sealed partial class ApplicationRunner
{
    private const string UpdateWord = "update";
    private const string CheckFlag = "--check";
    private const int CancelledExitCode = 130;

    private CancellationTokenSource? _updateCancellation;

    /// <summary>True when the command line is the <c>update</c> command (its word comes first).</summary>
    private static bool IsUpdate(string[] arguments)
    {
        return arguments.Length > 0 && arguments[0] == UpdateWord;
    }

    /// <summary>Runs <c>geeratya update [--check]</c> on the plain console, with no terminal UI.</summary>
    private int RunUpdate(string[] arguments)
    {
        var rest = arguments[1..];
        var unknown = rest.FirstOrDefault(argument => argument != CheckFlag);

        if (unknown is not null)
        {
            _error.WriteLine($"{ApplicationInfo.Name} {UpdateWord}: unknown argument '{unknown}'");
            _error.WriteLine(Usage);

            return 2;
        }

        var command = _update ?? UpdateCommand.CreateForProcess(
            _environment.Get(ReleaseClient.ReleasesUrlVariable),
            StateDirectory());

        using var cancellation = new CancellationTokenSource();

        _updateCancellation = cancellation;
        Console.CancelKeyPress += OnUpdateCancelKey;

        try
        {
            return command.RunAsync(rest.Contains(CheckFlag), _output, cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            _error.WriteLine("Cancelled.");

            return CancelledExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= OnUpdateCancelKey;
        }
    }

    // Ctrl-C ends the download cleanly instead of killing the process in the middle of a file operation.
    private void OnUpdateCancelKey(object? sender, ConsoleCancelEventArgs eventArguments)
    {
        eventArguments.Cancel = true;
        _updateCancellation?.Cancel();
    }

    private string? StateDirectory()
    {
        try
        {
            return new ApplicationDirectories(
                ApplicationDirectories.DetectOperatingSystem(),
                _environment,
                PlatformFolders.ForCurrentUser()).StateDirectory;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
