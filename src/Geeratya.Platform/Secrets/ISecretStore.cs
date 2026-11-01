namespace Geeratya.Platform.Secrets;

/// <summary>
/// The operating system's secret store. Keys are opaque identifiers; values are secrets and are never logged.
/// </summary>
public interface ISecretStore
{
    /// <summary>False when this system has no usable store; reads then find nothing and writes are refused.</summary>
    bool IsAvailable { get; }

    /// <summary>Returns the secret, or null when none is stored under the key (or no store is available).</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>Stores the secret; returns false when no store is available.</summary>
    Task<bool> SetAsync(string key, string secret, CancellationToken cancellationToken);

    /// <summary>Removes the secret if present.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
