using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Jira;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;
using Geeratya.Platform.Secrets;

namespace Geeratya.App;

/// <summary>
/// Runs the profile effects of the reducer (secret store and profile file) and answers with the command that
/// reports the result. Tokens are only handed to the store; no message ever contains one.
/// </summary>
public sealed class ProfileEffectHandler
{
    private const string NoStoreSeveralProfilesHint =
        "GEERATYA_TOKEN works for a single profile only, so several profiles need a secret store";

    private const string TokenVariableHint = "set GEERATYA_TOKEN to supply the token";

    private const string CacheLeftMessage = "Its cache folder could not be deleted completely.";

    private const string NoStoredTokenMessage = "Failed: no API token is stored for this profile; type one.";

    private static readonly TimeSpan _testTimeout = TimeSpan.FromSeconds(15);

    private readonly ProfileServices _services;
    private readonly ITransportFactory _transports;

    public ProfileEffectHandler(ProfileServices services, ITransportFactory? transports = null)
    {
        _services = services;
        _transports = transports ?? new DefaultTransportFactory();
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
                UpdateProfileEffect update => await UpdateAsync(update, cancellationToken),
                TestProfileEffect test => await TestAsync(test, cancellationToken),
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

        var cacheDeleted = _services.Caches.Delete(remove.Profile);

        DeleteHistory(remove.Profile);

        var message = RemovedMessage(remove);

        return new ProfilesChanged(remove.Catalog, cacheDeleted ? message : $"{message} {CacheLeftMessage}");
    }

    /// <summary>The history and board kinds are conveniences: a file that cannot be deleted is not reported.</summary>
    private void DeleteHistory(Profile profile)
    {
        DeleteQuietly(() => _services.BoardKinds.Delete(profile));
        DeleteQuietly(() => _services.Histories.Delete(profile));
        DeleteQuietly(() => _services.SavedViews?.Delete(profile));
        DeleteQuietly(() => _services.Tabs?.Delete(profile));
    }

    /// <summary>
    /// Saves an edited profile. A typed token is stored under the new key; a kept one is read from the old key and
    /// moved when the site or account changed, so the user never retypes it. The catalog is saved last: a failure
    /// before that leaves the old profile as it was.
    /// </summary>
    private async Task<Command> UpdateAsync(UpdateProfileEffect update, CancellationToken cancellationToken)
    {
        var store = _services.Tokens.Store;
        var keyChanged = update.Old.CredentialKey != update.New.CredentialKey;
        var token = update.NewToken?.Reveal();

        if (token is null && keyChanged)
        {
            token = await store.GetAsync(update.Old.CredentialKey, cancellationToken);

            if (token is null && !HasEnvironmentToken())
            {
                return new ProfileOperationFailed(
                    "The profile was not changed: no API token is stored for it, so type the token again.");
            }
        }

        var stored = true;

        if (token is not null)
        {
            stored = await store.SetAsync(update.New.CredentialKey, token, cancellationToken);
        }

        _services.Store.Save(update.Catalog);

        var leftover = string.Empty;

        if (keyChanged && !update.OldKeyShared)
        {
            leftover = await CleanUpOldAsync(update, cancellationToken);
        }

        var replaced = update.NewToken is not null && update.Catalog.ActiveName == update.New.Name;

        return new ProfilesChanged(update.Catalog, $"{UpdatedMessage(update, stored)}{leftover}", replaced);
    }

    /// <summary>The profile is already saved: a failing cleanup is reported, not turned into a failed save.</summary>
    private async Task<string> CleanUpOldAsync(UpdateProfileEffect update, CancellationToken cancellationToken)
    {
        _services.Caches.Delete(update.Old);
        DeleteHistory(update.Old);

        try
        {
            await _services.Tokens.Store.DeleteAsync(update.Old.CredentialKey, cancellationToken);
        }
        catch (SecretStoreException)
        {
            return " The old token could not be deleted from the secret store.";
        }

        return string.Empty;
    }

    private bool HasEnvironmentToken()
    {
        return _services.Tokens.HasEnvironmentToken;
    }

    private static string UpdatedMessage(UpdateProfileEffect update, bool stored)
    {
        if (!stored)
        {
            return $"Updated profile {update.New.Name}. No secret store is available: {TokenVariableHint}.";
        }

        return update.NewToken is null
            ? $"Updated profile {update.New.Name}; the stored token is kept."
            : $"Updated profile {update.New.Name}; the new token is in the secret store.";
    }

    /// <summary>Asks Jira who the account is with a throwaway transport; the token never leaves this method.</summary>
    private async Task<Command> TestAsync(TestProfileEffect test, CancellationToken cancellationToken)
    {
        var token = test.Token?.Reveal();

        if (token is null && test.StoredKey is not null)
        {
            var resolved = await _services.Tokens.ResolveAsync(
                test.StoredKey,
                test.AllowEnvironment,
                cancellationToken);

            token = resolved.Token;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new ProfileTested(test.Profile, false, NoStoredTokenMessage);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(_testTimeout);

        try
        {
            var connection = new JiraConnection(new Uri(test.Profile.SiteUrl), test.Profile.Email);

            using var transport = _transports.Create(connection, new JiraApiToken(token));
            var result = await new JiraUserClient(transport).GetMyselfAsync(timeout.Token);

            if (result.IsSuccess)
            {
                var name = ConnectionText.Sanitize(result.Value.DisplayName);

                return new ProfileTested(test.Profile, true, $"Connected as {name}.");
            }

            var failure = ConnectionText.Failure(result.Error, test.Profile.SiteUrl);

            return new ProfileTested(test.Profile, false, $"Failed: {failure.Hint}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProfileTested(test.Profile, false, "Failed: the site did not answer in time.");
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            return new ProfileTested(test.Profile, false, "Failed: the site address cannot be used.");
        }
    }

    private static void DeleteQuietly(Action delete)
    {
        try
        {
            delete();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Left behind in a hashed folder of the state directory; nothing else depends on it.
        }
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
