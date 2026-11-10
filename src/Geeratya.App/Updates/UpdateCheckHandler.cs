using Geeratya.Core;
using Geeratya.Core.State;
using Geeratya.Core.Updates;
using Geeratya.Platform;

namespace Geeratya.App.Updates;

/// <summary>
/// Runs <see cref="CheckForUpdateEffect"/> off the UI thread, after the first frame: removes a leftover old binary,
/// then looks for a newer release at most once a day. Reads nothing but one small file when the answer is cached;
/// writes only that file. A failed or disabled check says nothing.
/// </summary>
public sealed class UpdateCheckHandler : ILocalEffectHandler
{
    /// <summary>Set to anything but empty or <c>0</c> to turn the background check off.</summary>
    public const string DisableVariable = "GEERATYA_NO_UPDATE_CHECK";

    private readonly ReleaseClient _releases;
    private readonly UpdateCheckCache _cache;
    private readonly InstalledBinary _binary;
    private readonly EnvironmentReader _environment;
    private readonly string _currentVersion;

    public UpdateCheckHandler(
        ReleaseClient releases,
        UpdateCheckCache cache,
        InstalledBinary binary,
        EnvironmentReader environment,
        string? currentVersion = null)
    {
        _releases = releases;
        _cache = cache;
        _binary = binary;
        _environment = environment;
        _currentVersion = currentVersion ?? ApplicationInfo.Version;
    }

    /// <summary>The handler of the running process, answering from <paramref name="stateDirectory"/>.</summary>
    public static UpdateCheckHandler CreateForProcess(string stateDirectory)
    {
        var environment = EnvironmentReader.ForProcess();

        return new UpdateCheckHandler(
            new ReleaseClient(environment.Get(ReleaseClient.ReleasesUrlVariable)),
            new UpdateCheckCache(stateDirectory),
            InstalledBinary.ForProcess(),
            environment);
    }

    public bool CanRun(Effect effect)
    {
        return effect is CheckForUpdateEffect;
    }

    public async Task<Command?> RunAsync(Effect effect, CancellationToken cancellationToken)
    {
        _binary.RemoveLeftover();

        if (effect is not CheckForUpdateEffect { Enabled: true } || IsDisabledByEnvironment())
        {
            return null;
        }

        var current = ReleaseVersion.Parse(_currentVersion);

        // A development build has no release to compare with.
        if (current is null || current.IsDevelopment)
        {
            return null;
        }

        var latest = ReleaseVersion.Parse(await LatestAsync(cancellationToken));

        if (latest is null || !latest.IsNewerThan(current))
        {
            return null;
        }

        return new UpdateAvailable(new UpdateNotice(latest.Text, _binary.Route.Instructions));
    }

    private bool IsDisabledByEnvironment()
    {
        return _environment.Get(DisableVariable) is { } value && value != "0";
    }

    private async Task<string?> LatestAsync(CancellationToken cancellationToken)
    {
        if (_cache.FreshLatest() is { } cached)
        {
            return cached;
        }

        var lookup = await _releases.GetLatestAsync(cancellationToken);

        if (lookup.Release is not { } release)
        {
            return null;
        }

        _cache.Save(release.Version);

        return release.Version;
    }
}
