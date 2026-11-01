using Geeratya.Core.State;
using Geeratya.Platform.Secrets;

namespace Geeratya.Tui;

/// <summary>
/// Runs the profile effects of the reducer (secret store and profile file) and answers with the command that
/// reports the result. Tokens are only handed to the store; no message ever contains one.
/// </summary>
public sealed class ProfileEffectHandler
{
    private const string TokenVariableHint = "set GEERATYA_TOKEN to supply the token";

    private readonly ProfileServices _services;

    public ProfileEffectHandler(ProfileServices services)
    {
        _services = services;
    }

    /// <summary>The command that reports the outcome, or null when the effect is not a profile effect.</summary>
    public async Task<Command?> RunAsync(Effect effect, CancellationToken cancellationToken)
    {
        try
        {
            return effect switch
            {
                AddProfileEffect add => await AddAsync(add, cancellationToken),
                RemoveProfileEffect remove => await RemoveAsync(remove, cancellationToken),
                SaveProfilesEffect save => Save(save),
                _ => null,
            };
        }
        catch (SecretStoreException exception)
        {
            return new ProfileOperationFailed($"The secret store failed: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ProfileOperationFailed($"Could not write the profiles file: {exception.Message}");
        }
    }

    private async Task<Command> AddAsync(AddProfileEffect add, CancellationToken cancellationToken)
    {
        var stored = await _services.Tokens.Store.SetAsync(
            add.Profile.CredentialKey,
            add.Token.Reveal(),
            cancellationToken);

        _services.Store.Save(add.Catalog);

        var message = stored
            ? $"Added profile {add.Profile.Name}; the token is in the secret store."
            : $"Added profile {add.Profile.Name}. No secret store is available: {TokenVariableHint}.";

        return new ProfilesChanged(add.Catalog, message);
    }

    private async Task<Command> RemoveAsync(RemoveProfileEffect remove, CancellationToken cancellationToken)
    {
        if (remove.DeleteToken)
        {
            await _services.Tokens.Store.DeleteAsync(remove.Profile.CredentialKey, cancellationToken);
        }

        _services.Store.Save(remove.Catalog);

        return new ProfilesChanged(remove.Catalog, $"Removed profile {remove.Profile.Name}.");
    }

    private ProfilesChanged Save(SaveProfilesEffect save)
    {
        _services.Store.Save(save.Catalog);

        return new ProfilesChanged(save.Catalog, save.Message);
    }
}
