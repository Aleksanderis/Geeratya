using System.Text;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Secrets;

/// <summary>
/// The macOS Keychain through the <c>security</c> program (no native library, nothing to P/Invoke). Reading and
/// deleting pass only the key on the command line. Writing uses <c>security -i</c> and sends the command, token
/// included, on standard input, because <c>-w</c> on the command line would show the token in the process table.
/// </summary>
public sealed class MacOsSecretStore : ISecretStore
{
    private const string Program = "security";
    private const string ServiceName = "geeratya";
    private const string Label = "Geeratya Jira API token";
    private const int ItemNotFoundExitCode = 44;

    private readonly IProcessRunner _runner;

    public MacOsSecretStore(IProcessRunner runner)
    {
        _runner = runner;
    }

    public bool IsAvailable { get; private set; } = true;

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        string[] arguments = ["find-generic-password", "-s", ServiceName, "-a", key, "-w"];
        var result = await RunAsync(arguments, null, cancellationToken);

        if (result is not { ExitCode: 0 })
        {
            return null;
        }

        var output = result.StandardOutput;

        if (output.EndsWith('\n'))
        {
            output = output[..^1];
        }

        return output.Length == 0 ? null : output;
    }

    public async Task<bool> SetAsync(string key, string secret, CancellationToken cancellationToken)
    {
        if (HasControlCharacter(key) || HasControlCharacter(secret))
        {
            throw new SecretStoreException("The Keychain cannot store a value with control characters.");
        }

        var command = new StringBuilder("add-generic-password -U")
            .Append(" -s ").Append(Quote(ServiceName))
            .Append(" -a ").Append(Quote(key))
            .Append(" -l ").Append(Quote(Label))
            .Append(" -w ").Append(Quote(secret))
            .Append('\n')
            .ToString();
        var result = await RunAsync(["-i"], command, cancellationToken);

        if (result is null)
        {
            return false;
        }

        if (result.ExitCode != 0)
        {
            throw new SecretStoreException($"security could not store the token (exit code {result.ExitCode}).");
        }

        // The interactive mode may report a failed command on stderr and still exit 0, so read the value back.
        var stored = await GetAsync(key, cancellationToken);

        if (stored != secret)
        {
            throw new SecretStoreException("security did not store the token (locked or denied Keychain?).");
        }

        return true;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        string[] arguments = ["delete-generic-password", "-s", ServiceName, "-a", key];
        var result = await RunAsync(arguments, null, cancellationToken);

        if (result is { ExitCode: not (0 or ItemNotFoundExitCode) })
        {
            throw new SecretStoreException($"security could not remove the token (exit code {result.ExitCode}).");
        }
    }

    private static string Quote(string text)
    {
        var escaped = text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

        return "\"" + escaped + "\"";
    }

    private static bool HasControlCharacter(string text)
    {
        return text.Any(char.IsControl);
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
        catch (TimeoutException)
        {
            throw new SecretStoreException("security did not answer (locked Keychain or a prompt waiting?).");
        }
    }
}
