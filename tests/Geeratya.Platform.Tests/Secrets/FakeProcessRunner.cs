using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Tests.Secrets;

/// <summary>Records calls and returns a canned result, or reports the program as missing.</summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    public FakeProcessRunner(ProcessResult? result)
    {
        Result = result;
    }

    public ProcessResult? Result { get; }

    public List<IReadOnlyList<string>> Arguments { get; } = [];

    public List<string?> StandardInputs { get; } = [];

    public Task<ProcessResult> RunAsync(
        string program,
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        Arguments.Add(arguments);
        StandardInputs.Add(standardInput);

        if (Result is null)
        {
            throw new ProgramNotFoundException(program, new InvalidOperationException());
        }

        return Task.FromResult(Result);
    }
}
