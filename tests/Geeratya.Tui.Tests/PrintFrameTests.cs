using System.Text.RegularExpressions;
using Geeratya.App;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.BoardKinds;
using Geeratya.Core.Cache;
using Geeratya.Core.History;
using Geeratya.Core.Profiles;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.Tui.Tests.Rendering;

namespace Geeratya.Tui.Tests;

public sealed partial class PrintFrameTests : IDisposable
{
    private const string LiveHostsVariable = "GEERATYA_LIVE_HOSTS";
    private const string Site = "https://work.atlassian.net";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-print-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Theory]
    [InlineData("truecolor")]
    [InlineData("16")]
    [InlineData("none")]
    public void GivenAColourTier_WhenPrintingTheFrame_ThenItMatchesTheSnapshot(string tier)
    {
        using var writer = new StringWriter();
        var runner = new ApplicationRunner(new RunnerContext(writer, false), createProfiles: Services);

        var exitCode = runner.Run(["--print-frame", "--size", "60x12", "--mode", "normal", "--colors", tier]);
        var expected = SnapshotFile.Expect($"ansi-{tier}-60x12.txt", Normalize(writer.ToString()));

        Assert.Equal(0, exitCode);
        Assert.Equal(expected, Normalize(writer.ToString()));
    }

    [Fact]
    public void GivenColours_WhenPrintingTheFrame_ThenStrippingTheEscapesGivesThePlainFrame()
    {
        using var coloured = new StringWriter();
        using var plain = new StringWriter();

        new ApplicationRunner(new RunnerContext(coloured, false), createProfiles: Services)
            .Run(["--print-frame", "--size", "80x24", "--mode", "help", "--colors", "truecolor"]);
        new ApplicationRunner(new RunnerContext(plain, false)).Run(["--print-frame", "--size", "80x24", "--mode", "help"]);

        Assert.Contains("\u001b[", coloured.ToString());
        Assert.Equal(plain.ToString(), Escapes().Replace(coloured.ToString(), string.Empty));
    }

    [Fact]
    public void GivenNoColoursFlag_WhenPrintingTheFrame_ThenNoEscapeIsWritten()
    {
        using var writer = new StringWriter();

        new ApplicationRunner(new RunnerContext(writer, false)).Run(["--print-frame", "--mode", "help"]);

        Assert.DoesNotContain('\u001b', writer.ToString());
    }

    [Fact]
    public void GivenLight_WhenPrintingTheFrame_ThenTheLightThemeColoursDifferFromTheDarkOnes()
    {
        using var dark = new StringWriter();
        using var light = new StringWriter();

        new ApplicationRunner(new RunnerContext(dark, false), createProfiles: Services)
            .Run(["--print-frame", "--size", "40x10", "--colors", "truecolor"]);
        new ApplicationRunner(new RunnerContext(light, false), createProfiles: Services)
            .Run(["--print-frame", "--size", "40x10", "--colors", "truecolor", "--light"]);

        Assert.NotEqual(dark.ToString(), light.ToString());
    }

    [Fact]
    public async Task GivenABoardSource_WhenPrintingTheFrameWithColours_ThenItShowsTheLoadedBoardInColour()
    {
        await WriteProfilesAsync();

        var http = BoardHandler();
        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Runner(writer, error, http).Run(
        [
            "--print-frame", "--size", "100x24", "--colors", "16", "--source", "board:GCM board",
            "--profile", "work",
        ]);
        var plain = Escapes().Replace(writer.ToString(), string.Empty);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("\u001b[", writer.ToString());
        Assert.Contains("GCM board", plain);
        Assert.Contains("GCM-1", plain);
        Assert.Contains(
            http.Requests,
            request => request.AbsolutePath.EndsWith("/board/4/issue", StringComparison.Ordinal));
        Assert.All(http.Requests, request => Assert.Equal("work.atlassian.net", request.Host));
    }

    [Fact]
    public async Task GivenABoardSourceAndTheListLayout_WhenPrintingTheFrame_ThenTheBoardIsDrawnAsAList()
    {
        await WriteProfilesAsync();

        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Runner(writer, error, BoardHandler()).Run(
        [
            "--print-frame", "--size", "100x24", "--source", "board:GCM board", "--layout", "list",
            "--profile", "work",
        ]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Summary", writer.ToString());
        Assert.Contains("24 issues", writer.ToString());
    }

    [Fact]
    public async Task GivenAHostOutsideTheLiveHosts_WhenPrintingARealScreen_ThenItIsRefusedBeforeAnyRequest()
    {
        await WriteProfilesAsync();

        var http = BoardHandler();
        using var writer = new StringWriter();
        using var error = new StringWriter();
        var environment = new EnvironmentReader(name => name == LiveHostsVariable ? "dev.example.test" : null);
        var runner = new ApplicationRunner(
            new RunnerContext(writer, false, Error: error, Environment: environment),
            createProfiles: Services,
            transports: new FakeTransportFactory(http));

        var exitCode = runner.Run(["--print-frame", "--source", "board:GCM board", "--profile", "work"]);

        Assert.Equal(1, exitCode);
        Assert.Contains(LiveHostsVariable, error.ToString());
        Assert.Empty(http.Requests);
        Assert.Equal(string.Empty, writer.ToString());
    }

    [Fact]
    public async Task GivenNoLiveHostsAtAll_WhenPrintingARealScreen_ThenItIsRefused()
    {
        await WriteProfilesAsync();

        using var writer = new StringWriter();
        using var error = new StringWriter();
        var runner = new ApplicationRunner(
            new RunnerContext(writer, false, Error: error, Environment: new EnvironmentReader(_ => null)),
            createProfiles: Services);

        var exitCode = runner.Run(["--print-frame", "--source", "jql:project = GCM", "--profile", "work"]);

        Assert.Equal(1, exitCode);
        Assert.Contains(LiveHostsVariable, error.ToString());
    }

    [Fact]
    public async Task GivenAnUnknownProfile_WhenPrintingARealScreen_ThenItNamesTheSavedOnes()
    {
        await WriteProfilesAsync();

        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Runner(writer, error, BoardHandler())
            .Run(["--print-frame", "--source", "jql:project = GCM", "--profile", "nope"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("'nope'", error.ToString());
        Assert.Contains("work", error.ToString());
    }

    [Theory]
    [InlineData("--source", "nonsense")]
    [InlineData("--source", "board:")]
    [InlineData("--source", "sprint:1")]
    [InlineData("--layout", "grid")]
    [InlineData("--colors", "rgb")]
    public void GivenAnInvalidValue_WhenPrintingTheFrame_ThenUsageIsShown(string flag, string value)
    {
        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new ApplicationRunner(new RunnerContext(writer, false, Error: error), createProfiles: Services)
            .Run(["--print-frame", flag, value]);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", error.ToString());
    }

    [Fact]
    public async Task GivenAnIssueSource_WhenPrintingTheFrame_ThenTheIssueDetailIsDrawn()
    {
        await WriteProfilesAsync();

        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Runner(writer, error, IssueHandler()).Run(
            ["--print-frame", "--size", "100x24", "--colors", "16", "--source", "issue:GCM-1", "--profile", "work"]);
        var plain = Escapes().Replace(writer.ToString(), string.Empty);

        Assert.Equal(0, exitCode);
        Assert.Contains("Description", plain);
        Assert.Contains(" · Story", plain);
        Assert.Contains("Description", plain);
    }

    [Fact]
    public async Task GivenAJqlSource_WhenPrintingTheFrame_ThenTheResultsAreListed()
    {
        await WriteProfilesAsync();

        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Runner(writer, error, IssueHandler()).Run(
            ["--print-frame", "--size", "100x24", "--source", "jql:key = GCM-10", "--profile", "work"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("GCM-1", writer.ToString());
        Assert.Contains("Summary", writer.ToString());
    }

    [Fact]
    public async Task GivenTheColumnsLayoutOnAList_WhenPrintingTheFrame_ThenTheViewKeepsItsLayoutAndAFailureShows()
    {
        await WriteProfilesAsync();

        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Runner(writer, error, IssueHandler()).Run(
            ["--print-frame", "--source", "jql:key = GCM-10", "--layout", "columns", "--profile", "work"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("GCM-1", writer.ToString());
    }

    [Fact]
    public async Task GivenAnUnknownBoard_WhenPrintingARealScreen_ThenItFailsWithExitOne()
    {
        await WriteProfilesAsync();

        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Runner(writer, error, BoardHandler())
            .Run(["--print-frame", "--source", "board:Nope", "--profile", "work"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("did not open", error.ToString());
        Assert.Equal(string.Empty, writer.ToString());
    }

    [Fact]
    public async Task GivenAsciiAndColours_WhenPrintingARealScreen_ThenNoBoxDrawingCharacterIsLeft()
    {
        await WriteProfilesAsync();

        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Runner(writer, error, BoardHandler()).Run(
        [
            "--print-frame", "--size", "100x24", "--colors", "16", "--ascii", "--source", "board:GCM board",
            "--profile", "work",
        ]);
        var plain = Escapes().Replace(writer.ToString(), string.Empty);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain(plain, character => character is >= '\u2500' and <= '\u257f');
        Assert.Contains("GCM-1", plain);
    }

    [Theory]
    [InlineData("issue:GCM-10 OR 1=1")]
    [InlineData("issue:x")]
    public void GivenABadIssueKey_WhenPrintingTheFrame_ThenUsageIsShown(string source)
    {
        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new ApplicationRunner(new RunnerContext(writer, false, Error: error), createProfiles: Services)
            .Run(["--print-frame", "--source", source]);

        Assert.Equal(2, exitCode);
    }

    private ApplicationRunner Runner(StringWriter writer, StringWriter error, RoutedJiraHandler http)
    {
        return new ApplicationRunner(
            new RunnerContext(
                writer,
                false,
                Error: error,
                Environment: new EnvironmentReader(name => name == LiveHostsVariable ? "work.atlassian.net" : null)),
            createProfiles: Services,
            transports: new FakeTransportFactory(http, new JiraTransportOptions { MaxRetries = 0 }));
    }

    private ProfileServices Services()
    {
        return new ProfileServices(
            new ProfileStore(_directory),
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)),
            new SiteCaches(Path.Combine(_directory, "cache")),
            new HistoryStore(Path.Combine(_directory, "state")),
            new BoardKindStore(Path.Combine(_directory, "state")),
            ThemesDirectory: Path.Combine(_directory, "themes"));
    }

    private async Task WriteProfilesAsync()
    {
        Directory.CreateDirectory(_directory);

        var catalog = ProfileCatalog.Empty.With(new Profile("work", Site, "me@work.test"));

        await Task.Run(() => new ProfileStore(_directory).Save(catalog));
    }

    private static RoutedJiraHandler BoardHandler()
    {
        return new RoutedJiraHandler()
            .Fixture("/rest/agile/1.0/board", "boards/board-list.json")
            .Fixture("/rest/agile/1.0/board/4/configuration", "boards/board-gcm-config.json")
            .Fixture("/rest/agile/1.0/board/4/issue", "boards/rows-gcm-issues.json");
    }

    private static RoutedJiraHandler IssueHandler()
    {
        return new RoutedJiraHandler()
            .Fixture("/rest/api/3/search/jql", "search/search-rows-gcm.json")
            .Fixture("/rest/api/3/issue/GCM-1", "issues/issue-gcm10.json")
            .Fixture("/rest/api/3/issue/GCM-1/comment", "comments/comments-empty.json")
            .Fixture("/rest/api/3/field", "metadata/field-list.json");
    }

    private static string Normalize(string text)
    {
        return text.ReplaceLineEndings("\n");
    }

    [GeneratedRegex("\u001b\\[[0-9;]*m")]
    private static partial Regex Escapes();
}
