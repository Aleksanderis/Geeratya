using System.ComponentModel;
using System.Diagnostics;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Editors;

/// <summary>
/// Runs a program with the console of this process inherited (Linux and macOS: the controlling terminal; Windows:
/// the console), so a full-screen editor works. Cancellation kills the process tree.
/// </summary>
public sealed class TerminalProgramRunner : ITerminalProgramRunner
{
    public async Task<int> RunAsync(
        string program,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(program, arguments);

        using var process = Start(startInfo);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            KillQuietly(process);

            throw;
        }

        return process.ExitCode;
    }

    private static ProcessStartInfo CreateStartInfo(string program, IReadOnlyList<string> arguments)
    {
        if (OperatingSystem.IsWindows() && WindowsProgramResolver.IsBatchFile(program))
        {
            // A batch file cannot be started directly; cmd.exe runs it and needs its own quoting, so the argument
            // string is built by hand.
            return new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                Arguments = CmdCommandLine.Build(program, arguments),
            };
        }

        var startInfo = new ProcessStartInfo(program) { UseShellExecute = false };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It ended on its own just before.
        }
    }

    private static Process Start(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException("The process did not start.");
        }
        catch (Win32Exception exception)
        {
            throw new ProgramNotFoundException(startInfo.FileName, exception);
        }
    }
}
