using Geeratya.Platform.Secrets;

namespace Geeratya.App.PrintFrame;

/// <summary>
/// A secret store that answers every key with the same placeholder and stores nothing, so the print frame never reads
/// the OS secret store or <c>GEERATYA_TOKEN</c>. The fake Jira ignores credentials.
/// </summary>
public sealed class FixedSecretStore : ISecretStore
{
    private const string Placeholder = "print-frame";

    public bool IsAvailable => true;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(Placeholder);
    }

    public Task<bool> SetAsync(string key, string secret, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
