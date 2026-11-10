using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.App.Updates;
using Geeratya.Core.State;
using Geeratya.Core.Updates;
using Geeratya.Platform;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Updates;

namespace Geeratya.App.Tests.Updates;

public sealed class UpdateCheckHandlerTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("geeratya-check-test-");
    private readonly FakeReleaseServer _server = new();
    private readonly ManualTimeProvider _time = new();

    public void Dispose()
    {
        _directory.Delete(true);
    }

    [Fact]
    public async Task GivenANewerRelease_WhenChecking_ThenTheNoticeCarriesTheVersionAndTheScriptCommand()
    {
        _server.ServeRelease("v1.3.0");

        var reply = await Check(Handler(Script()), enabled: true);

        var notice = Assert.IsType<UpdateAvailable>(reply).Notice;

        Assert.Equal("1.3.0", notice.Version);
        Assert.Equal("run `geeratya update`", notice.Instructions);
    }

    [Fact]
    public async Task GivenAPackageManagedBinary_WhenChecking_ThenTheNoticeNamesThePackageCommand()
    {
        _server.ServeRelease("v1.3.0");

        var binary = new InstalledBinary(
            OperatingSystemKind.MacOs,
            "/opt/homebrew/bin/geeratya",
            new PlatformFolders("/Users/ann", "/Users/ann", "/Users/ann"));

        var reply = await Check(Handler(binary), enabled: true);

        Assert.Contains("brew upgrade geeratya", Assert.IsType<UpdateAvailable>(reply).Notice.Instructions);
    }

    [Theory]
    [InlineData("v1.2.0")]
    [InlineData("v1.0.0")]
    [InlineData("not-a-version")]
    public async Task GivenNoNewerRelease_WhenChecking_ThenThereIsNoReply(string tag)
    {
        _server.ServeRelease(tag);

        Assert.Null(await Check(Handler(Script()), enabled: true));
    }

    [Fact]
    public async Task GivenTheSettingIsOff_WhenChecking_ThenNothingIsRequested()
    {
        _server.ServeRelease("v1.3.0");

        Assert.Null(await Check(Handler(Script()), enabled: false));
        Assert.Empty(_server.Requests);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("yes")]
    public async Task GivenTheEnvironmentVariable_WhenChecking_ThenNothingIsRequested(string value)
    {
        _server.ServeRelease("v1.3.0");

        var reply = await Check(Handler(Script(), value), enabled: true);

        Assert.Null(reply);
        Assert.Empty(_server.Requests);
    }

    [Fact]
    public async Task GivenTheVariableIsZero_WhenChecking_ThenTheCheckRuns()
    {
        _server.ServeRelease("v1.3.0");

        Assert.NotNull(await Check(Handler(Script(), "0"), enabled: true));
    }

    [Fact]
    public async Task GivenADevelopmentBuild_WhenChecking_ThenNothingIsRequested()
    {
        _server.ServeRelease("v1.3.0");

        var reply = await Check(Handler(Script(), currentVersion: "0.0.0-dev"), enabled: true);

        Assert.Null(reply);
        Assert.Empty(_server.Requests);
    }

    [Fact]
    public async Task GivenAnAnswerFromLessThanADayAgo_WhenChecking_ThenGitHubIsNotAskedAgainButTheNoticeStays()
    {
        _server.ServeRelease("v1.3.0");

        var handler = Handler(Script());

        await Check(handler, enabled: true);
        _time.Advance(TimeSpan.FromHours(23));

        var reply = await Check(handler, enabled: true);

        Assert.Single(_server.Requests);
        Assert.Equal("1.3.0", Assert.IsType<UpdateAvailable>(reply).Notice.Version);
    }

    [Fact]
    public async Task GivenAnAnswerFromMoreThanADayAgo_WhenChecking_ThenGitHubIsAskedAgain()
    {
        _server.ServeRelease("v1.3.0");

        var handler = Handler(Script());

        await Check(handler, enabled: true);
        _time.Advance(TimeSpan.FromHours(25));
        _server.ServeRelease("v1.4.0");

        var reply = await Check(handler, enabled: true);

        Assert.Equal(2, _server.Requests.Count);
        Assert.Equal("1.4.0", Assert.IsType<UpdateAvailable>(reply).Notice.Version);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GivenGitHubFails_WhenChecking_ThenThereIsNoReplyAndNothingIsCached(HttpStatusCode status)
    {
        _server.Answer($"{FakeReleaseServer.BaseUrl}/latest", status);

        Assert.Null(await Check(Handler(Script()), enabled: true));
        Assert.Null(new UpdateCheckCache(_directory.FullName, _time).FreshLatest());
    }

    [Fact]
    public async Task GivenGarbageInsteadOfJson_WhenChecking_ThenThereIsNoReply()
    {
        _server.Serve($"{FakeReleaseServer.BaseUrl}/latest", "<html>proxy login</html>");

        Assert.Null(await Check(Handler(Script()), enabled: true));
    }

    [Fact]
    public async Task GivenACheck_WhenRequesting_ThenOnlyAUserAgentGoesAlong()
    {
        _server.ServeRelease("v1.3.0");

        await Check(Handler(Script()), enabled: true);

        var headers = Assert.Single(_server.Headers);

        Assert.Null(headers.Authorization);
        Assert.Null(headers.Referrer);
        Assert.Equal("Geeratya", Assert.Single(headers.UserAgent).Product?.Name);
        Assert.False(headers.Contains("Cookie"));
    }

    [Fact]
    public async Task GivenALeftoverOldBinaryOnWindows_WhenTheCheckIsOff_ThenItIsStillRemoved()
    {
        var executable = Path.Combine(_directory.FullName, "geeratya.exe");
        var leftover = ExecutableSwap.OldPath(executable);

        File.WriteAllText(leftover, "stale");

        var binary = new InstalledBinary(
            OperatingSystemKind.Windows,
            executable,
            new PlatformFolders("C:\\Users\\ann", "C:\\Users\\ann", _directory.FullName));

        await Check(Handler(binary), enabled: false);

        Assert.False(File.Exists(leftover));
    }

    [Fact]
    public void GivenTheEffect_WhenAsking_ThenOnlyTheUpdateCheckIsClaimed()
    {
        var handler = Handler(Script());

        Assert.True(handler.CanRun(new CheckForUpdateEffect(true)));
        Assert.False(handler.CanRun(new QuitEffect()));
    }

    private InstalledBinary Script()
    {
        var path = Path.Combine(_directory.FullName, "geeratya");

        File.WriteAllText(Path.Combine(_directory.FullName, InstallRouteDetector.MarkerFileName), "script");

        return new InstalledBinary(
            OperatingSystemKind.Linux,
            path,
            new PlatformFolders("/home/ann", "/home/ann/.config", "/home/ann/.local"));
    }

    private UpdateCheckHandler Handler(
        InstalledBinary binary,
        string? disableValue = null,
        string currentVersion = "1.2.0")
    {
        var environment = new EnvironmentReader(
            name => name == UpdateCheckHandler.DisableVariable ? disableValue : null);

        return new UpdateCheckHandler(
            new ReleaseClient(FakeReleaseServer.BaseUrl, _server),
            new UpdateCheckCache(_directory.FullName, _time),
            binary,
            environment,
            currentVersion);
    }

    private static Task<Command?> Check(UpdateCheckHandler handler, bool enabled)
    {
        return handler.RunAsync(new CheckForUpdateEffect(enabled), CancellationToken.None);
    }
}
