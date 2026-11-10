using Geeratya.Core;
using Geeratya.Core.Updates;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Updates;
using System.Runtime.InteropServices;

namespace Geeratya.App.Updates;

/// <summary>
/// <c>geeratya update [--check]</c>, run before and outside the terminal UI. It never needs PowerShell or any
/// other program: the binary asks GitHub and downloads the archive itself. A script install is replaced in place;
/// a package-managed or unrecognised install gets the matching instructions and no file is touched.
/// Exit codes: 0 up to date (or updated, or instructions printed), 1 a newer release exists (<c>--check</c>),
/// 2 failure.
/// </summary>
public sealed class UpdateCommand : IUpdateCommand
{
    public const int UpToDate = 0;
    public const int UpdateExists = 1;
    public const int Failed = 2;

    private readonly ReleaseClient _releases;
    private readonly InstalledBinary _binary;
    private readonly UpdateInstaller _installer;
    private readonly UpdateCheckCache? _cache;
    private readonly string _currentVersion;

    public UpdateCommand(
        ReleaseClient releases,
        InstalledBinary binary,
        UpdateInstaller installer,
        UpdateCheckCache? cache = null,
        string? currentVersion = null)
    {
        _releases = releases;
        _binary = binary;
        _installer = installer;
        _cache = cache;
        _currentVersion = currentVersion ?? ApplicationInfo.Version;
    }

    /// <summary>The command wired for the running process.</summary>
    public static UpdateCommand CreateForProcess(string? releasesUrl, string? stateDirectory)
    {
        var binary = InstalledBinary.ForProcess();
        var releases = new ReleaseClient(releasesUrl);
        var target = ReleaseTarget.Of(binary.OperatingSystem, RuntimeInformation.OSArchitecture);
        var installer = new UpdateInstaller(releases, new ProcessRunner(), binary, target);
        var cache = stateDirectory is null ? null : new UpdateCheckCache(stateDirectory);

        return new UpdateCommand(releases, binary, installer, cache);
    }

    public async Task<int> RunAsync(bool checkOnly, TextWriter output, CancellationToken cancellationToken)
    {
        var current = ReleaseVersion.Parse(_currentVersion);

        if (current is null || current.IsDevelopment)
        {
            output.WriteLine($"This is a development build ({_currentVersion}); there is no release to update to.");

            return UpToDate;
        }

        var lookup = await _releases.GetLatestAsync(cancellationToken);

        if (lookup.Release is not { } release)
        {
            output.WriteLine($"Could not look for a newer release: {lookup.Error}");
            output.WriteLine($"Check by hand: {InstallRouteDetector.ReleasesPage}");

            return Failed;
        }

        _cache?.Save(release.Version);

        if (ReleaseVersion.Parse(release.Version) is not { } latest || !latest.IsNewerThan(current))
        {
            output.WriteLine($"Geeratya {current.Text} is up to date.");

            return UpToDate;
        }

        output.WriteLine($"Geeratya {latest.Text} is available (installed: {current.Text}).");

        if (checkOnly)
        {
            output.WriteLine($"To update: {_binary.Route.Instructions}");

            return UpdateExists;
        }

        return _binary.Route.Kind == InstallRouteKind.Script
            ? await InstallAsync(release, current.Text, output, cancellationToken)
            : PrintInstructions(output);
    }

    private int PrintInstructions(TextWriter output)
    {
        output.WriteLine($"This install is not managed by `geeratya update`; to update: {_binary.Route.Instructions}");

        return UpToDate;
    }

    private async Task<int> InstallAsync(
        ReleaseInfo release,
        string installed,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        try
        {
            await _installer.InstallAsync(release, output, cancellationToken);
        }
        catch (UpdateFailedException exception)
        {
            PrintManualSteps(release, exception.Message, output);

            return Failed;
        }

        output.WriteLine($"Updated Geeratya {installed} -> {release.Version} ({_binary.Path}).");
        output.WriteLine($"Source: {InstallRouteDetector.ReleasesPage}");

        return UpToDate;
    }

    private void PrintManualSteps(ReleaseInfo release, string reason, TextWriter output)
    {
        var url = _installer.FindArchive(release)?.Url ?? InstallRouteDetector.ReleasesPage;
        var binaryName = Path.GetFileName(_binary.Path);

        output.WriteLine($"The update failed: {reason}");
        output.WriteLine("Your installed Geeratya was not changed. To update by hand:");
        output.WriteLine($"  1. Download {url}");
        output.WriteLine($"  2. Unpack it and replace {_binary.Path} with the {binaryName} inside.");
    }
}
