using System.Security.Cryptography;
using Geeratya.Core;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Updates;

namespace Geeratya.App.Updates;

/// <summary>
/// Replaces the running binary with the one of a release: downloads the archive and <c>SHA256SUMS</c>, refuses a
/// download that does not match its checksum, unpacks the program next to the installed one, runs it with
/// <c>--version</c> and only when it reports the release's version swaps it in. Until the swap the installed binary
/// is untouched; a failure at any step leaves it as it was.
/// </summary>
public sealed class UpdateInstaller
{
    private const string ChecksumsName = "SHA256SUMS";
    private const string VersionFlag = "--version";

    private readonly ReleaseClient _releases;
    private readonly IProcessRunner _processes;
    private readonly InstalledBinary _binary;
    private readonly ReleaseTarget? _target;

    /// <param name="releases">Downloads the files.</param>
    /// <param name="processes">Runs the new binary for its version check.</param>
    /// <param name="binary">The binary to replace.</param>
    /// <param name="target">The archive made for this system; null when no release is built for it.</param>
    public UpdateInstaller(
        ReleaseClient releases,
        IProcessRunner processes,
        InstalledBinary binary,
        ReleaseTarget? target)
    {
        _releases = releases;
        _processes = processes;
        _binary = binary;
        _target = target;
    }

    /// <summary>The archive of <paramref name="release"/> for this system, or null.</summary>
    public ReleaseAsset? FindArchive(ReleaseInfo release)
    {
        return _target is null ? null : release.FindBySuffix(_target.AssetSuffix);
    }

    /// <summary>Installs <paramref name="release"/>; progress lines go to <paramref name="output"/>.</summary>
    /// <exception cref="UpdateFailedException">The update could not be done; the message says why.</exception>
    public async Task InstallAsync(ReleaseInfo release, TextWriter output, CancellationToken cancellationToken)
    {
        if (_target is null)
        {
            throw new UpdateFailedException("no release is built for this system");
        }

        if (_binary.Path.Length == 0)
        {
            throw new UpdateFailedException("the system does not say where this program is installed");
        }

        var archive = FindArchive(release)
            ?? throw new UpdateFailedException($"release {release.Tag} has no {_target.Platform} archive");
        var checksums = release.Assets.FirstOrDefault(asset => asset.Name == ChecksumsName)
            ?? throw new UpdateFailedException(
                $"release {release.Tag} has no {ChecksumsName}; refusing to install an unverified download");

        using var work = new WorkFolder();
        var archivePath = Path.Combine(work.Path, Path.GetFileName(archive.Name));
        var checksumsPath = Path.Combine(work.Path, ChecksumsName);

        output.WriteLine($"Downloading {archive.Name}");
        await DownloadAsync(archive.Url, archivePath, cancellationToken);
        await DownloadAsync(checksums.Url, checksumsPath, cancellationToken);
        await VerifyChecksumAsync(archive.Name, archivePath, checksumsPath, cancellationToken);

        var staged = ExecutableSwap.StagedPath(_binary.Path);

        try
        {
            Stage(archivePath, staged);
            await VerifyVersionAsync(staged, release.Version, cancellationToken);
            Swap(staged);
        }
        finally
        {
            DeleteQuietly(staged);
        }
    }

    private async Task DownloadAsync(string url, string destination, CancellationToken cancellationToken)
    {
        try
        {
            await _releases.DownloadAsync(url, destination, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException)
        {
            throw new UpdateFailedException($"download failed ({url}): {exception.Message}", exception);
        }
    }

    private static async Task VerifyChecksumAsync(
        string name,
        string archivePath,
        string checksumsPath,
        CancellationToken cancellationToken)
    {
        var expected = ChecksumFile.HashOf(await File.ReadAllTextAsync(checksumsPath, cancellationToken), name)
            ?? throw new UpdateFailedException($"{ChecksumsName} has no entry for {name}");

        await using var stream = File.OpenRead(archivePath);

        var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));

        if (actual != expected)
        {
            throw new UpdateFailedException($"checksum mismatch for {name} (expected {expected}, got {actual})");
        }
    }

    private void Stage(string archivePath, string staged)
    {
        try
        {
            ArchiveExtractor.ExtractProgram(archivePath, _target!, staged);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UpdateFailedException($"cannot write next to {_binary.Path}: {exception.Message}", exception);
        }
        catch (IOException exception)
        {
            throw new UpdateFailedException($"cannot write next to {_binary.Path}: {exception.Message}", exception);
        }
    }

    private async Task VerifyVersionAsync(string staged, string version, CancellationToken cancellationToken)
    {
        var swap = new ExecutableSwap(_binary.OperatingSystem);

        swap.MakeExecutable(staged);

        ProcessResult result;

        try
        {
            result = await _processes.RunAsync(staged, [VersionFlag], null, cancellationToken);
        }
        catch (Exception exception) when (exception is ProgramNotFoundException or TimeoutException or IOException)
        {
            throw new UpdateFailedException($"the new program does not start: {exception.Message}", exception);
        }

        var words = result.StandardOutput.Split(['\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries);

        if (result.ExitCode != 0 || words.Length < 2 || words[0] != ApplicationInfo.Name || words[1] != version)
        {
            throw new UpdateFailedException(
                $"the new program did not report version {version} for {VersionFlag}; the installed one is kept");
        }
    }

    private void Swap(string staged)
    {
        try
        {
            new ExecutableSwap(_binary.OperatingSystem).Replace(_binary.Path, staged);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new UpdateFailedException($"could not replace {_binary.Path}: {exception.Message}", exception);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A staged file that cannot go is only clutter next to the binary.
        }
    }

    /// <summary>A temporary folder that is removed when disposed.</summary>
    private sealed class WorkFolder : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("geeratya-update-");

        public string Path => _directory.FullName;

        public void Dispose()
        {
            try
            {
                _directory.Delete(true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Left in the system's temporary folder, which it cleans.
            }
        }
    }
}
