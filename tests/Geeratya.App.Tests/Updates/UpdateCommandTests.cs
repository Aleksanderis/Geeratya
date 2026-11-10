using System.Runtime.InteropServices;
using Geeratya.App.Tests.Fakes;
using Geeratya.App.Updates;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Updates;

namespace Geeratya.App.Tests.Updates;

public sealed class UpdateCommandTests : IDisposable
{
    private const string Archive = "geeratya-1.3.0-linux-x64.tar.gz";
    private const string WindowsArchive = "geeratya-1.3.0-win-x64.zip";
    private const string Sums = "SHA256SUMS";

    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("geeratya-update-test-");
    private readonly FakeReleaseServer _server = new();
    private readonly StringWriter _output = new();

    public void Dispose()
    {
        _output.Dispose();
        _directory.Delete(true);
    }

    [Fact]
    public async Task GivenANewerRelease_WhenCheckingOnly_ThenExitIsOneAndNothingIsDownloadedOrChanged()
    {
        var path = Install("geeratya", "script");

        _server.ServeRelease("v1.3.0", Archive, Sums);

        var exitCode = await Run(path, OperatingSystemKind.Linux, checkOnly: true);

        Assert.Equal(UpdateCommand.UpdateExists, exitCode);
        Assert.Contains("1.3.0 is available (installed: 1.2.0)", _output.ToString(), StringComparison.Ordinal);
        Assert.Equal("old binary", File.ReadAllText(path));
        Assert.Single(_server.Requests);
    }

    [Theory]
    [InlineData("v1.2.0")]
    [InlineData("v1.1.9")]
    public async Task GivenTheSameOrAnOlderRelease_WhenUpdating_ThenExitIsZeroAndTheBinaryIsKept(string tag)
    {
        var path = Install("geeratya", "script");

        _server.ServeRelease(tag, Archive, Sums);

        var exitCode = await Run(path, OperatingSystemKind.Linux, checkOnly: false);

        Assert.Equal(UpdateCommand.UpToDate, exitCode);
        Assert.Contains("up to date", _output.ToString(), StringComparison.Ordinal);
        Assert.Single(_server.Requests);
    }

    [Fact]
    public async Task GivenAScriptInstallOnLinux_WhenUpdating_ThenTheVerifiedBinaryReplacesTheOldOne()
    {
        var path = Install("geeratya", "script");
        var archive = ReleaseArchives.TarGz("geeratya-1.3.0-linux-x64", "geeratya", "new binary");

        Serve("v1.3.0", Archive, archive);

        var processes = new FakeVersionProcessRunner("geeratya 1.3.0\n");
        var exitCode = await Run(path, OperatingSystemKind.Linux, checkOnly: false, processes: processes);

        Assert.Equal(UpdateCommand.UpToDate, exitCode);
        Assert.Equal("new binary", File.ReadAllText(path));
        Assert.Equal("new binary", Assert.Single(processes.StartedContents));
        Assert.Contains("Updated Geeratya 1.2.0 -> 1.3.0", _output.ToString(), StringComparison.Ordinal);
        Assert.Equal([path, Path.Combine(_directory.FullName, Marker)], Directory.GetFiles(_directory.FullName).Order());
    }

    [Fact]
    public async Task GivenAScriptInstallOnWindows_WhenUpdating_ThenTheZipIsUnpackedAndTheExeSwappedWithoutPowerShell()
    {
        var path = Install("geeratya.exe", "script");
        var archive = ReleaseArchives.Zip("geeratya-1.3.0-win-x64", "geeratya.exe", "new exe");

        Serve("v1.3.0", WindowsArchive, archive);

        var exitCode = await Run(path, OperatingSystemKind.Windows, checkOnly: false, target: WindowsTarget());

        Assert.Equal(UpdateCommand.UpToDate, exitCode);
        Assert.Equal("new exe", File.ReadAllText(path));
        Assert.DoesNotContain(
            Directory.GetFiles(_directory.FullName),
            file => file.EndsWith(".new.exe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenAChecksumThatDoesNotMatch_WhenUpdating_ThenTheBinaryIsKeptAndTheManualStepsAreShown()
    {
        var path = Install("geeratya", "script");
        var archive = ReleaseArchives.TarGz("geeratya-1.3.0-linux-x64", "geeratya", "tampered");

        _server.ServeRelease("v1.3.0", Archive, Sums);
        _server.Serve(FakeReleaseServer.DownloadUrl(Archive), archive);
        _server.Serve(FakeReleaseServer.DownloadUrl(Sums), ReleaseArchives.Sums(Archive, [1, 2, 3]));

        var processes = new FakeVersionProcessRunner("geeratya 1.3.0\n");
        var exitCode = await Run(path, OperatingSystemKind.Linux, checkOnly: false, processes: processes);

        Assert.Equal(UpdateCommand.Failed, exitCode);
        Assert.Equal("old binary", File.ReadAllText(path));
        Assert.Empty(processes.StartedContents);
        Assert.Contains("checksum mismatch", _output.ToString(), StringComparison.Ordinal);
        Assert.Contains(FakeReleaseServer.DownloadUrl(Archive), _output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("at Geeratya", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnInterruptedDownload_WhenUpdating_ThenTheBinaryIsKeptAndTheUrlIsPrinted()
    {
        var path = Install("geeratya", "script");

        _server.ServeRelease("v1.3.0", Archive, Sums);
        _server.Interrupt(FakeReleaseServer.DownloadUrl(Archive));

        var exitCode = await Run(path, OperatingSystemKind.Linux, checkOnly: false);

        Assert.Equal(UpdateCommand.Failed, exitCode);
        Assert.Equal("old binary", File.ReadAllText(path));
        Assert.Contains("download failed", _output.ToString(), StringComparison.Ordinal);
        Assert.Contains(FakeReleaseServer.DownloadUrl(Archive), _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAReleaseWithoutChecksums_WhenUpdating_ThenItRefusesTheUnverifiedDownload()
    {
        var path = Install("geeratya", "script");

        _server.ServeRelease("v1.3.0", Archive);
        _server.Serve(FakeReleaseServer.DownloadUrl(Archive), ReleaseArchives.TarGz("f", "geeratya", "new binary"));

        var exitCode = await Run(path, OperatingSystemKind.Linux, checkOnly: false);

        Assert.Equal(UpdateCommand.Failed, exitCode);
        Assert.Equal("old binary", File.ReadAllText(path));
        Assert.Contains("unverified", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenANewBinaryThatReportsAnotherVersion_WhenUpdating_ThenTheOldOneStays()
    {
        var path = Install("geeratya", "script");
        var archive = ReleaseArchives.TarGz("geeratya-1.3.0-linux-x64", "geeratya", "new binary");

        Serve("v1.3.0", Archive, archive);

        var exitCode = await Run(
            path,
            OperatingSystemKind.Linux,
            checkOnly: false,
            processes: new FakeVersionProcessRunner("geeratya 0.0.1\n"));

        Assert.Equal(UpdateCommand.Failed, exitCode);
        Assert.Equal("old binary", File.ReadAllText(path));
        Assert.DoesNotContain(
            Directory.GetFiles(_directory.FullName),
            file => file.EndsWith(".new", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenAnArchiveWithoutTheProgram_WhenUpdating_ThenTheOldOneStays()
    {
        var path = Install("geeratya", "script");
        var archive = ReleaseArchives.TarGz("geeratya-1.3.0-linux-x64", "other-file", "x");

        Serve("v1.3.0", Archive, archive);

        var exitCode = await Run(path, OperatingSystemKind.Linux, checkOnly: false);

        Assert.Equal(UpdateCommand.Failed, exitCode);
        Assert.Equal("old binary", File.ReadAllText(path));
        Assert.Contains("holds no geeratya", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAPackageManagedInstall_WhenUpdating_ThenTheCommandIsPrintedAndNothingIsDownloaded()
    {
        var path = Install("geeratya", marker: null);
        var facts = new InstalledBinary(
            OperatingSystemKind.MacOs,
            "/opt/homebrew/Cellar/geeratya/1.2.0/bin/geeratya",
            Folders());

        _server.ServeRelease("v1.3.0", Archive, Sums);

        var exitCode = await Run(facts, checkOnly: false);

        Assert.Equal(UpdateCommand.UpToDate, exitCode);
        Assert.Contains("brew upgrade geeratya", _output.ToString(), StringComparison.Ordinal);
        Assert.Single(_server.Requests);
        Assert.Equal("old binary", File.ReadAllText(path));
    }

    [Fact]
    public async Task GivenGitHubIsUnreachable_WhenChecking_ThenExitIsTwoWithTheReasonAndNoStackTrace()
    {
        var path = Install("geeratya", "script");

        _server.Interrupt($"{FakeReleaseServer.BaseUrl}/latest");

        var exitCode = await Run(path, OperatingSystemKind.Linux, checkOnly: true);

        Assert.Equal(UpdateCommand.Failed, exitCode);
        Assert.Contains("Could not look for a newer release", _output.ToString(), StringComparison.Ordinal);
        Assert.Contains(InstallRouteDetector.ReleasesPage, _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenADevelopmentBuild_WhenUpdating_ThenNoRequestIsMade()
    {
        var path = Install("geeratya", "script");
        var binary = new InstalledBinary(OperatingSystemKind.Linux, path, Folders());
        var command = Command(binary, null, currentVersion: "0.0.0-dev");

        var exitCode = await command.RunAsync(false, _output, CancellationToken.None);

        Assert.Equal(UpdateCommand.UpToDate, exitCode);
        Assert.Empty(_server.Requests);
        Assert.Contains("development build", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACheck_WhenGitHubAnswers_ThenTheAnswerIsCachedForTheBackgroundCheck()
    {
        var path = Install("geeratya", "script");
        var cache = new UpdateCheckCache(_directory.FullName);

        _server.ServeRelease("v1.3.0", Archive, Sums);

        await Run(path, OperatingSystemKind.Linux, checkOnly: true, cache: cache);

        Assert.Equal("1.3.0", cache.FreshLatest());
    }

    private const string Marker = InstallRouteDetector.MarkerFileName;

    private static ReleaseTarget LinuxTarget()
    {
        return ReleaseTarget.Of(OperatingSystemKind.Linux, Architecture.X64)!;
    }

    private static ReleaseTarget WindowsTarget()
    {
        return ReleaseTarget.Of(OperatingSystemKind.Windows, Architecture.X64)!;
    }

    private PlatformFolders Folders()
    {
        return new PlatformFolders("/home/ann", "/home/ann/.config", _directory.FullName);
    }

    private string Install(string name, string? marker)
    {
        var path = Path.Combine(_directory.FullName, name);

        File.WriteAllText(path, "old binary");

        if (marker is not null)
        {
            File.WriteAllText(Path.Combine(_directory.FullName, Marker), marker);
        }

        return path;
    }

    private void Serve(string tag, string archiveName, byte[] archive)
    {
        _server.ServeRelease(tag, archiveName, Sums);
        _server.Serve(FakeReleaseServer.DownloadUrl(archiveName), archive);
        _server.Serve(FakeReleaseServer.DownloadUrl(Sums), ReleaseArchives.Sums(archiveName, archive));
    }

    private Task<int> Run(
        string path,
        OperatingSystemKind system,
        bool checkOnly,
        FakeVersionProcessRunner? processes = null,
        ReleaseTarget? target = null,
        UpdateCheckCache? cache = null)
    {
        var binary = new InstalledBinary(system, path, Folders());

        return Run(binary, checkOnly, processes, target, cache);
    }

    private Task<int> Run(
        InstalledBinary binary,
        bool checkOnly,
        FakeVersionProcessRunner? processes = null,
        ReleaseTarget? target = null,
        UpdateCheckCache? cache = null)
    {
        return Command(binary, cache, "1.2.0", processes, target).RunAsync(checkOnly, _output, CancellationToken.None);
    }

    private UpdateCommand Command(
        InstalledBinary binary,
        UpdateCheckCache? cache,
        string currentVersion,
        IProcessRunner? processes = null,
        ReleaseTarget? target = null)
    {
        var releases = new ReleaseClient(FakeReleaseServer.BaseUrl, _server);
        var installer = new UpdateInstaller(
            releases,
            processes ?? new FakeVersionProcessRunner("geeratya 1.3.0\n"),
            binary,
            target ?? LinuxTarget());

        return new UpdateCommand(releases, binary, installer, cache, currentVersion);
    }
}
