using System.ComponentModel;
using System.Diagnostics;

namespace Geeratya.Platform.Processes;

/// <summary>
/// Runs programs with <see cref="Process"/>; arguments are passed as a list, never through a shell. Cancellation
/// and a 30 second timeout kill the process tree; a timeout throws <see cref="TimeoutException"/>.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

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
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(_timeout);

        try
        {
            return await CommunicateAsync(process, standardInput, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException($"Program '{program}' did not finish within {_timeout.TotalSeconds:0} seconds.");
        }
    }

    public Task<int> RunDetachedAsync(
        string program,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return RunDetachedAsync(program, arguments, null, cancellationToken);
    }

    public async Task<int> RunDetachedAsync(
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
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(_timeout);

        if (standardInput is not null)
        {
            await WriteInputAsync(process, standardInput, timeout.Token);
        }

        process.StandardInput.Close();

        try
        {
            // No stream is read, so a browser started by the opener (which inherits the pipes) cannot block this.
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Program '{program}' did not finish within {_timeout.TotalSeconds:0} seconds.");
        }

        return process.ExitCode;
    }

    private static async Task<ProcessResult> CommunicateAsync(
        Process process,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        if (standardInput is not null)
        {
            await WriteInputAsync(process, standardInput, cancellationToken);
        }

        process.StandardInput.Close();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        await errorTask;

        return new ProcessResult(process.ExitCode, output);
    }

    private static async Task WriteInputAsync(Process process, string standardInput, CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        catch (IOException)
        {
            // The program exited before reading its input; its exit code tells the story.
        }
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
