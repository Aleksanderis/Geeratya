using Geeratya.Core.State;
using Geeratya.Platform.Secrets;

namespace Geeratya.Tui;

/// <summary>
/// Runs the profile effects of the reducer (secret store and profile file) and answers with the command that
/// reports the result. Tokens are only handed to the store; no message ever contains one.
/// </summary>
public sealed class ProfileEffectHandler
{
    private const string NoStoreSeveralProfilesHint =
        "GEERATYA_TOKEN works for a single profile only, so several profiles need a secret store";

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
            return new ProfileOperationFailed(
                $"Could not write the profiles file: {exception.Message} (a token stored for this profile stays in the secret store)");
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            return new ProfileOperationFailed($"The profile change failed: {exception.GetType().Name}.");
        }
    }

    private async Task<Command> AddAsync(AddProfileEffect add, CancellationToken cancellationToken)
    {
        var stored = await _services.Tokens.Store.SetAsync(
            add.Profile.CredentialKey,
            add.Token.Reveal(),
            cancellationToken);

        _services.Store.Save(add.Catalog);

        return new ProfilesChanged(add.Catalog, AddedMessage(add, stored));
    }

    private async Task<Command> RemoveAsync(RemoveProfileEffect remove, CancellationToken cancellationToken)
    {
        if (remove.DeleteToken)
        {
            await _services.Tokens.Store.DeleteAsync(remove.Profile.CredentialKey, cancellationToken);
        }

        _services.Store.Save(remove.Catalog);

        return new ProfilesChanged(remove.Catalog, RemovedMessage(remove));
    }

    private static string RemovedMessage(RemoveProfileEffect remove)
    {
        if (!remove.WasActive)
        {
            return $"Removed profile {remove.Profile.Name}.";
        }

        var next = remove.Catalog.ActiveName;

        return next is null
            ? $"Removed profile {remove.Profile.Name}. No profile is active."
            : $"Removed profile {remove.Profile.Name}. {next} is now the active profile.";
    }

    private ProfilesChanged Save(SaveProfilesEffect save)
    {
        _services.Store.Save(save.Catalog);

        return new ProfilesChanged(save.Catalog, save.Message);
    }

    private static string AddedMessage(AddProfileEffect add, bool stored)
    {
        if (stored)
        {
            return $"Added profile {add.Profile.Name}; the token is in the secret store.";
        }

        var hint = add.Catalog.Profiles.Count > 1 ? NoStoreSeveralProfilesHint : TokenVariableHint;

        return $"Added profile {add.Profile.Name}. No secret store is available: {hint}.";
    }
}
