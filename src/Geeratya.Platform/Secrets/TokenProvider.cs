namespace Geeratya.Platform.Secrets;

/// <summary>Finds the API token for a credential key: the secret store first, then <c>GEERATYA_TOKEN</c>.
/// The variable is one token for the whole process, so the caller allows it only where it cannot reach another site.</summary>
public sealed class TokenProvider
{
    public const string EnvironmentVariable = "GEERATYA_TOKEN";

    private readonly EnvironmentReader _environment;

    public TokenProvider(ISecretStore store, EnvironmentReader environment)
    {
        Store = store;
        _environment = environment;
    }

    public ISecretStore Store { get; }

    /// <summary>Whether <c>GEERATYA_TOKEN</c> is set (its value is never exposed here).</summary>
    public bool HasEnvironmentToken => _environment.Get(EnvironmentVariable) is not null;

    public async Task<TokenResolution> ResolveAsync(
        string credentialKey,
        bool allowEnvironment,
        CancellationToken cancellationToken)
    {
        var stored = await Store.GetAsync(credentialKey, cancellationToken);

        if (stored is not null)
        {
            return new TokenResolution(stored, TokenSource.SecretStore);
        }

        if (!allowEnvironment)
        {
            return new TokenResolution(null, TokenSource.None);
        }

        var fromEnvironment = _environment.Get(EnvironmentVariable);

        if (fromEnvironment is null)
        {
            return new TokenResolution(null, TokenSource.None);
        }

        return new TokenResolution(fromEnvironment, TokenSource.EnvironmentVariable);
    }
}
