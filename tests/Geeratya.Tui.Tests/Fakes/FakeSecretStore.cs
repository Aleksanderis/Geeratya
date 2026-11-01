using Geeratya.Platform.Secrets;

namespace Geeratya.Tui.Tests.Fakes;

/// <summary>In-memory secret store that can also pretend to be missing or broken.</summary>
public sealed class FakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = [];
    private readonly bool _throwsCancellation;
    private readonly bool _throwsUnexpected;

    public FakeSecretStore(
        bool isAvailable = true,
        bool fails = false,
        bool throwsCancellation = false,
        bool throwsUnexpected = false)
    {
        _throwsCancellation = throwsCancellation;
        _throwsUnexpected = throwsUnexpected;
        IsAvailable = isAvailable;
        Fails = fails;
    }

    public bool IsAvailable { get; }

    public bool Fails { get; }

    public IReadOnlyDictionary<string, string> Secrets => _secrets;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        return Task.FromResult(_secrets.GetValueOrDefault(key));
    }

    public Task<bool> SetAsync(string key, string secret, CancellationToken cancellationToken)
    {
        if (_throwsCancellation)
        {
            throw new OperationCanceledException();
        }

        if (_throwsUnexpected)
        {
            throw new InvalidOperationException("boom");
        }

        if (Fails)
        {
            throw new SecretStoreException("keyring locked");
        }

        if (!IsAvailable)
        {
            return Task.FromResult(false);
        }

        _secrets[key] = secret;

        return Task.FromResult(true);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _secrets.Remove(key);

        return Task.CompletedTask;
    }
}
