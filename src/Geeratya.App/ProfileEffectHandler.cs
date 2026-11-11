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
public sealed class ProfileEffectHandler : ILocalEffectHandler
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

    public bool CanRun(Effect effect)
    {
        return effect is AddProfileEffect or RemoveProfileEffect or SaveProfilesEffect or UpdateProfileEffect
            or TestProfileEffect or LoadTokenSourcesEffect;
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
                LoadTokenSourcesEffect sources => await TokenSourcesAsync(sources, cancellationToken),
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
                $"Could not write the profiles file: {exception.Message}");
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

        var failure = await SaveRollingBackAsync(add.Catalog, add.Profile, stored);

        if (failure is not null)
        {
            return failure;
        }

        return new ProfilesChanged(add.Catalog, AddedMessage(add, stored));
    }

    /// <summary>
    /// Saves the catalog. When the file cannot be written and a token was just stored under the profile's key, the
    /// token would belong to no profile, so it is deleted again unless another profile of the catalog uses the same
    /// key. Returns the failure to report, or null when the catalog was saved.
    /// </summary>
    private async Task<ProfileOperationFailed?> SaveRollingBackAsync(
        ProfileCatalog catalog,
        Profile profile,
        bool tokenStored)
    {
        try
        {
            _services.Store.Save(catalog);

            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var left = tokenStored && !await TryDeleteOrphanTokenAsync(catalog, profile);
            var hint = left ? " (a token stored for this profile stays in the secret store)" : string.Empty;

            return new ProfileOperationFailed($"Could not write the profiles file: {exception.Message}{hint}");
        }
    }

    /// <summary>True when no orphan token is left: deleted, or kept because another profile shares the key.</summary>
    private async Task<bool> TryDeleteOrphanTokenAsync(ProfileCatalog catalog, Profile profile)
    {
        var key = profile.CredentialKey;

        if (catalog.Profiles.Any(other => other.Name != profile.Name && other.CredentialKey == key))
        {
            return true;
        }

        using var timeout = new CancellationTokenSource(_testTimeout);

        try
        {
            await _services.Tokens.Store.DeleteAsync(key, timeout.Token);

            return true;
        }
        catch (Exception exception) when (exception is SecretStoreException or OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Finds where each profile's token comes from for the profile manager. The token is read and dropped here: only
    /// its source goes back. The environment variable counts for a single profile only, as for the connection.
    /// </summary>
    private async Task<Command> TokenSourcesAsync(LoadTokenSourcesEffect effect, CancellationToken cancellationToken)
    {
        var allowEnvironment = effect.Profiles.Count == 1;
        var sources = new Dictionary<string, TokenSourceKind>();

        foreach (var profile in effect.Profiles)
        {
            sources[profile.CredentialKey] = await SourceOfAsync(profile, allowEnvironment, cancellationToken);
        }

        return new TokenSourcesLoaded(sources);
    }

    /// <summary>A failing secret store is not a failed profile operation: the pane says the source is unavailable.</summary>
    private async Task<TokenSourceKind> SourceOfAsync(
        Profile profile,
        bool allowEnvironment,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolved = await _services.Tokens.ResolveAsync(profile.CredentialKey, allowEnvironment, cancellationToken);

            return resolved.Source switch
            {
                TokenSource.SecretStore => TokenSourceKind.SecretStore,
                TokenSource.EnvironmentVariable => TokenSourceKind.EnvironmentVariable,
                _ => TokenSourceKind.None,
            };
        }
        catch (SecretStoreException)
        {
            return TokenSourceKind.Unavailable;
        }
    }

    private async Task<Command> RemoveAsync(RemoveProfileEffect remove, CancellationToken cancellationToken)
    {
        _services.Store.Save(remove.Catalog);

        var tokenLeft = false;

        if (remove.DeleteToken)
        {
            try
            {
                await _services.Tokens.Store.DeleteAsync(remove.Profile.CredentialKey, cancellationToken);
            }
            catch (SecretStoreException)
            {
                tokenLeft = true;
            }
        }

        var cacheDeleted = _services.Caches.Delete(remove.Profile);

        DeleteHistory(remove.Profile);

        var message = RemovedMessage(remove);

        if (tokenLeft)
        {
            message = $"{message} Its token could not be deleted from the secret store.";
        }

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

        var rollback = stored && token is not null && keyChanged;
        var failure = await SaveRollingBackAsync(update.Catalog, update.New, rollback);

        if (failure is not null)
        {
            return failure;
        }

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
