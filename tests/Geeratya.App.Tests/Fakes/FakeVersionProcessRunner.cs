using Geeratya.Platform.Processes;

namespace Geeratya.App.Tests.Fakes;

/// <summary>Answers the <c>--version</c> run of a freshly unpacked binary with a canned text and records the program.</summary>
public sealed class FakeVersionProcessRunner : IProcessRunner
{
    private readonly ProcessResult _result;

    private readonly Exception? _failure;

    public FakeVersionProcessRunner(string standardOutput, int exitCode = 0, Exception? failure = null)
    {
        _result = new ProcessResult(exitCode, standardOutput);
        _failure = failure;
    }

    /// <summary>The programs started, with the content of the file at that moment.</summary>
    public List<string> StartedContents { get; } = [];

    public Task<ProcessResult> RunAsync(
        string program,
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        StartedContents.Add(File.ReadAllText(program));

        if (_failure is not null)
        {
            throw _failure;
        }

        return Task.FromResult(_result);
    }

    public Task<int> RunDetachedAsync(
        string program,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<int> RunDetachedAsync(
        string program,
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}
