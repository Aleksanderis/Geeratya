using Geeratya.Platform.Secrets;

namespace Geeratya.Platform.Tests.Secrets;

/// <summary>In-memory secret store for tests.</summary>
public sealed class FakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = [];

    public bool IsAvailable => true;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        return Task.FromResult(_secrets.GetValueOrDefault(key));
    }

    public Task<bool> SetAsync(string key, string secret, CancellationToken cancellationToken)
    {
        _secrets[key] = secret;

        return Task.FromResult(true);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _secrets.Remove(key);

        return Task.CompletedTask;
    }
}
