namespace Geeratya.Platform.Secrets;

/// <summary>
/// Used where no secret store exists (an unknown system): the token then comes from the <c>GEERATYA_TOKEN</c>
/// environment variable.
/// </summary>
public sealed class UnsupportedSecretStore : ISecretStore
{
    public bool IsAvailable => false;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(null);
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
