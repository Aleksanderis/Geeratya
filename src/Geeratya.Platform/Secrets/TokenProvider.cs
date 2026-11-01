namespace Geeratya.Platform.Secrets;

/// <summary>Finds the API token for a credential key: the secret store first, then <c>GEERATYA_TOKEN</c>.</summary>
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

    public async Task<TokenResolution> ResolveAsync(string credentialKey, CancellationToken cancellationToken)
    {
        var stored = await Store.GetAsync(credentialKey, cancellationToken);

        if (stored is not null)
        {
            return new TokenResolution(stored, TokenSource.SecretStore);
        }

        var fromEnvironment = _environment.Get(EnvironmentVariable);

        if (fromEnvironment is null)
        {
            return new TokenResolution(null, TokenSource.None);
        }

        return new TokenResolution(fromEnvironment, TokenSource.EnvironmentVariable);
    }
}
