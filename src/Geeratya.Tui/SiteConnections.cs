using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Jira;
using Geeratya.Jira.Transport;
using Geeratya.Platform.Secrets;

namespace Geeratya.Tui;

/// <summary>
/// Owns the one Jira transport of the active profile. A switch disposes the old transport and builds a new one from
/// the new profile's site, account and token; an answer to an older request is recognised by its generation and
/// discarded. Several views share <see cref="Current"/>; nothing here is global.
/// </summary>
public sealed class SiteConnections : IDisposable
{
    private const string NoTokenMessage =
        "No API token stored for this profile: add the profile again to store one.";

    private const string NoTokenSingleProfileMessage =
        $"No API token for this profile: add it again or set {TokenProvider.EnvironmentVariable}.";

    private const string BadSiteMessage = "The profile's site URL cannot be used: it must be https.";

    private readonly TokenProvider _tokens;
    private readonly Func<JiraConnection, JiraApiToken, JiraTransport> _createTransport;
    private readonly object _lock = new();
    private Profile? _profile;
    private JiraTransport? _transport;
    private int _generation;

    public SiteConnections(
        TokenProvider tokens,
        Func<JiraConnection, JiraApiToken, JiraTransport>? createTransport = null)
    {
        _tokens = tokens;
        _createTransport = createTransport ?? CreateDefault;
    }

    /// <summary>The transport of the active profile once it is ready, otherwise null.</summary>
    public JiraTransport? Current
    {
        get
        {
            lock (_lock)
            {
                return _transport;
            }
        }
    }

    /// <summary>Counts every switch; a reply that started under another value is stale.</summary>
    public int Generation
    {
        get
        {
            lock (_lock)
            {
                return _generation;
            }
        }
    }

    /// <summary>Whether a reply that started under <paramref name="generation"/> still belongs to the site.</summary>
    public bool IsCurrent(int generation)
    {
        return Generation == generation;
    }

    /// <summary>
    /// Makes <paramref name="profile"/> the connected profile (null disconnects). Does nothing when it already is.
    /// The synchronous part only swaps state; the token lookup runs off the calling thread. Returns the failure to
    /// report, or null. <paramref name="profileCount"/> is the number of saved profiles: the process-wide
    /// <c>GEERATYA_TOKEN</c> is used only when it is 1, so one site's token is never sent to another profile's host.
    /// </summary>
    public Task<ConnectionFailed?> ActivateAsync(
        Profile? profile,
        int profileCount,
        CancellationToken cancellationToken)
    {
        int generation;

        lock (_lock)
        {
            if (profile == _profile)
            {
                return Task.FromResult<ConnectionFailed?>(null);
            }

            generation = Replace(profile);
        }

        if (profile is null)
        {
            return Task.FromResult<ConnectionFailed?>(null);
        }

        return ConnectAsync(profile, profileCount == 1, generation, cancellationToken);
    }

    /// <summary>Closes the connection of <paramref name="profile"/> if it is the connected one.</summary>
    public void Drop(Profile profile)
    {
        lock (_lock)
        {
            if (profile == _profile)
            {
                Replace(null);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            Replace(null);
        }
    }

    private static JiraTransport CreateDefault(JiraConnection connection, JiraApiToken token)
    {
        return new JiraTransport(connection, token);
    }

    private async Task<ConnectionFailed?> ConnectAsync(
        Profile profile,
        bool allowEnvironment,
        int generation,
        CancellationToken cancellationToken)
    {
        TokenResolution resolution;

        try
        {
            resolution = await Task.Run(
                () => _tokens.ResolveAsync(profile.CredentialKey, allowEnvironment, cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (SecretStoreException exception)
        {
            return new ConnectionFailed(profile, $"The secret store failed: {exception.Message}");
        }

        if (string.IsNullOrWhiteSpace(resolution.Token))
        {
            return new ConnectionFailed(profile, allowEnvironment ? NoTokenSingleProfileMessage : NoTokenMessage);
        }

        JiraTransport transport;

        try
        {
            var connection = new JiraConnection(new Uri(profile.SiteUrl), profile.Email);

            transport = _createTransport(connection, new JiraApiToken(resolution.Token));
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            return new ConnectionFailed(profile, BadSiteMessage);
        }

        lock (_lock)
        {
            if (generation == _generation)
            {
                _transport = transport;

                return null;
            }
        }

        transport.Dispose();

        return null;
    }

    private int Replace(Profile? profile)
    {
        _transport?.Dispose();
        _transport = null;
        _profile = profile;
        _generation++;

        return _generation;
    }
}
