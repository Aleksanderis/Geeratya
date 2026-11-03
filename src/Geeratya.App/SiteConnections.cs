using Geeratya.Core.BoardKinds;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Jira;
using Geeratya.Jira.Sprints;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform.Secrets;

namespace Geeratya.App;

/// <summary>
/// Owns the one Jira transport of the active profile. A switch disposes the old transport and builds a new one from
/// the new profile's site, account and token; an answer to an older request is recognised by its generation and
/// discarded. Several views share <see cref="Current"/>; nothing here is global.
/// </summary>
public sealed class SiteConnections : IDisposable
{
    private const string NoTokenMessage =
        "No API token stored for this profile: remove and add it again to store one.";

    private const string NoTokenSingleProfileMessage =
        $"No API token for this profile: add it again or set {TokenProvider.EnvironmentVariable}.";

    private const string BadSiteMessage = "The profile's site URL cannot be used: it must be https.";

    private readonly TokenProvider _tokens;
    private readonly ITransportFactory _transports;
    private readonly BoardKindStore? _boardKinds;
    private readonly object _lock = new();
    private Profile? _profile;
    private JiraTransport? _transport;
    private BoardKindResolver? _kinds;
    private Profile? _failedProfile;
    private int _failedProfileCount;
    private int _generation;
    private Task _connecting = Task.CompletedTask;

    public SiteConnections(
        TokenProvider tokens,
        ITransportFactory? transports = null,
        BoardKindStore? boardKinds = null)
    {
        _boardKinds = boardKinds;
        _tokens = tokens;
        _transports = transports ?? new DefaultTransportFactory();
    }

    /// <summary>
    /// Raised when a rate-limit wait of the current transport starts (with its end) or ends (null). It runs on a
    /// non-UI thread; handlers only post to the UI.
    /// </summary>
    public event Action<DateTimeOffset?>? RateLimitChanged;

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

    /// <summary>
    /// The transport and the board-kind resolver of the active profile, read together so they belong to one
    /// connection. The resolver is seeded from the profile's stored answers and stores each new one.
    /// </summary>
    internal (JiraTransport? Transport, BoardKindResolver? Kinds) Snapshot()
    {
        lock (_lock)
        {
            return (_transport, _kinds);
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
            if (profile == _profile || (profile is not null && profile == _failedProfile
                && profileCount == _failedProfileCount))
            {
                return Task.FromResult<ConnectionFailed?>(null);
            }

            generation = Replace(profile);
        }

        if (profile is null)
        {
            return Task.FromResult<ConnectionFailed?>(null);
        }

        var connecting = ConnectAsync(profile, profileCount, generation, cancellationToken);

        lock (_lock)
        {
            if (generation == _generation)
            {
                _connecting = connecting;
            }
        }

        return connecting;
    }

    /// <summary>
    /// Ends when the connection being built (token lookup, transport) is ready or has failed; already complete when
    /// none is being built. A check waits on this so it never runs before the transport exists.
    /// </summary>
    public Task WhenReadyAsync()
    {
        lock (_lock)
        {
            return _connecting;
        }
    }

    /// <summary>
    /// Forgets that the active profile failed to connect, so the next <see cref="ActivateAsync"/> tries again. Without
    /// it a failed profile is retried only when the profile or the number of saved profiles changes, which stops the
    /// failure reply from re-triggering the connect that produced it.
    /// </summary>
    public void RetryFailed()
    {
        lock (_lock)
        {
            _failedProfile = null;
        }
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

    private async Task<ConnectionFailed?> ConnectAsync(
        Profile profile,
        int profileCount,
        int generation,
        CancellationToken cancellationToken)
    {
        var allowEnvironment = profileCount == 1;
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
            ForgetFailed(profile, profileCount);

            return new ConnectionFailed(profile, $"The secret store failed: {exception.Message}");
        }

        if (string.IsNullOrWhiteSpace(resolution.Token))
        {
            ForgetFailed(profile, profileCount);

            return new ConnectionFailed(profile, allowEnvironment ? NoTokenSingleProfileMessage : NoTokenMessage);
        }

        JiraTransport transport;

        try
        {
            var connection = new JiraConnection(new Uri(profile.SiteUrl), profile.Email);

            transport = _transports.Create(connection, new JiraApiToken(resolution.Token));
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            ForgetFailed(profile, profileCount);

            return new ConnectionFailed(profile, BadSiteMessage);
        }

        lock (_lock)
        {
            if (generation == _generation)
            {
                _transport = transport;
                _kinds = CreateResolver(profile, transport);
                transport.RateLimitWaitChanged += () => OnRateLimitWaitChanged(generation, transport);

                return null;
            }
        }

        transport.Dispose();

        return null;
    }

    private BoardKindResolver CreateResolver(Profile profile, JiraTransport transport)
    {
        var resolver = new BoardKindResolver(
            new SprintClient(transport),
            (boardId, kind) => SaveKind(profile, boardId, kind));

        if (_boardKinds is not null)
        {
            foreach (var (boardId, kind) in _boardKinds.Load(profile))
            {
                resolver.Remember(boardId, kind);
            }
        }

        return resolver;
    }

    /// <summary>The answers are a convenience: a file that cannot be written only means one more probe next run.</summary>
    private void SaveKind(Profile profile, long boardId, BoardKind kind)
    {
        try
        {
            _boardKinds?.Save(profile, boardId, kind);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not reported: see above.
        }
    }

    private void OnRateLimitWaitChanged(int generation, JiraTransport transport)
    {
        if (IsCurrent(generation))
        {
            RateLimitChanged?.Invoke(transport.RateLimitedUntil);
        }
    }

    // A failed profile is not "connected", so activating it again (for example after another profile was removed and
    // the environment token became usable) tries once more.
    private void ForgetFailed(Profile profile, int profileCount)
    {
        lock (_lock)
        {
            if (profile == _profile && _transport is null)
            {
                _profile = null;
                _failedProfile = profile;
                _failedProfileCount = profileCount;
            }
        }
    }

    private int Replace(Profile? profile)
    {
        var old = _transport;

        _transport = null;
        _kinds = null;
        _profile = profile;
        _failedProfile = null;
        _generation++;

        // Last: disposing cancels requests in flight, and their continuations may run right here (same thread, inside
        // the lock). The generation has to be the new one by then, or such a reply would be posted as current.
        old?.Dispose();

        return _generation;
    }
}
