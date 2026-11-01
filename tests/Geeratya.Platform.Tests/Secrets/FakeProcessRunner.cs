using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Tests.Secrets;

/// <summary>Records calls and returns a canned result, or reports the program as missing.</summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    public FakeProcessRunner(ProcessResult result)
        : this(result, null)
    {
    }

    private FakeProcessRunner(ProcessResult? result, Exception? failure)
    {
        Result = result;
        Failure = failure;
    }

    public ProcessResult? Result { get; }

    public static FakeProcessRunner Missing()
    {
        return new FakeProcessRunner(null, null);
    }

    public static FakeProcessRunner Failing(Exception failure)
    {
        return new FakeProcessRunner(null, failure);
    }

    public Exception? Failure { get; }

    public List<IReadOnlyList<string>> Arguments { get; } = [];

    public List<string?> StandardInputs { get; } = [];

    public async Task<int> RunDetachedAsync(
        string program,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(program, arguments, null, cancellationToken);

        return result.ExitCode;
    }

    public Task<ProcessResult> RunAsync(
        string program,
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        Arguments.Add(arguments);
        StandardInputs.Add(standardInput);

        if (Failure is not null)
        {
            throw Failure;
        }

        if (Result is null)
        {
            throw new ProgramNotFoundException(program, new InvalidOperationException());
        }

        return Task.FromResult(Result);
    }
}
