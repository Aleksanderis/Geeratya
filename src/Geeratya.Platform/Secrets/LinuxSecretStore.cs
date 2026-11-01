using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Secrets;

/// <summary>
/// libsecret through the <c>secret-tool</c> program. The secret goes to its standard input, never into the
/// argument list (which other users can read in the process table).
/// </summary>
public sealed class LinuxSecretStore : ISecretStore
{
    private const string Program = "secret-tool";
    private const string ServiceAttribute = "service";
    private const string ServiceName = "geeratya";
    private const string AccountAttribute = "account";
    private const string Label = "--label=Geeratya Jira API token";

    private readonly IProcessRunner _runner;

    public LinuxSecretStore(IProcessRunner runner)
    {
        _runner = runner;
    }

    public bool IsAvailable { get; private set; } = true;

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        string[] arguments = ["lookup", ServiceAttribute, ServiceName, AccountAttribute, key];
        var result = await RunAsync(arguments, null, cancellationToken);

        if (result is not { ExitCode: 0 } || result.StandardOutput.Length == 0)
        {
            return null;
        }

        return result.StandardOutput;
    }

    public async Task<bool> SetAsync(string key, string secret, CancellationToken cancellationToken)
    {
        string[] arguments = ["store", Label, ServiceAttribute, ServiceName, AccountAttribute, key];
        var result = await RunAsync(arguments, secret, cancellationToken);

        if (result is null)
        {
            return false;
        }

        if (result.ExitCode != 0)
        {
            throw new SecretStoreException($"secret-tool could not store the token (exit code {result.ExitCode}).");
        }

        return true;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        string[] arguments = ["clear", ServiceAttribute, ServiceName, AccountAttribute, key];

        await RunAsync(arguments, null, cancellationToken);
    }

    private async Task<ProcessResult?> RunAsync(
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _runner.RunAsync(Program, arguments, standardInput, cancellationToken);
        }
        catch (ProgramNotFoundException)
        {
            IsAvailable = false;

            return null;
        }
    }
}
