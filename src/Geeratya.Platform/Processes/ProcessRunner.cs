using System.ComponentModel;
using System.Diagnostics;

namespace Geeratya.Platform.Processes;

/// <summary>Runs programs with <see cref="Process"/>; arguments are passed as a list, never through a shell.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string program,
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(program)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = StartProcess(startInfo);

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
        }

        process.StandardInput.Close();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        await errorTask;

        return new ProcessResult(process.ExitCode, output);
    }

    private static Process StartProcess(ProcessStartInfo startInfo)
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
