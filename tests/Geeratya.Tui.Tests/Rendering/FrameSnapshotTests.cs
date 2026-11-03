namespace Geeratya.Tui.Tests.Rendering;

public class FrameSnapshotTests
{
    private static readonly string[] _modes = ["normal", "help", "command", "confirm", "insert"];
    private static readonly string[] _sizes = ["120x36", "80x24", "40x10"];

    private static readonly string[] _sections =
        ["Global", "All actions", "Commands", "Issue", "Comment", "Rank", "View", "Tabs", "Navigate", "General", "Dialogs"];

    private static readonly int[] _scrolls = [0, 20, 42];

    public static TheoryData<string, string> Snapshots
    {
        get
        {
            var data = new TheoryData<string, string>();

            foreach (var mode in _modes)
            {
                foreach (var size in _sizes)
                {
                    data.Add(mode, size);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Snapshots))]
    public void GivenModeAndSize_WhenPrintingTheFrame_ThenItMatchesTheSnapshot(string mode, string size)
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run(["--print-frame", "--size", size, "--mode", mode]);
        var expected = SnapshotFile.Expect($"{mode}-{size}.txt", writer.ToString());

        Assert.Equal(0, exitCode);
        Assert.Equal(Normalize(expected), Normalize(writer.ToString()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(42)]
    public void GivenHelpScrolled_WhenPrintingTheFrame_ThenItMatchesTheSnapshotThatShowsTheLaterGroups(int scroll)
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run(
        [
            "--print-frame", "--size", "120x36", "--mode", "help", "--help-page", "1", "--help-scroll",
            scroll.ToString(),
        ]);
        var expected = SnapshotFile.Expect($"help-all-scroll{scroll}-120x36.txt", writer.ToString());

        Assert.Equal(0, exitCode);
        Assert.Equal(Normalize(expected), Normalize(writer.ToString()));
    }

    [Fact]
    public void GivenTheCommandsPage_WhenPrintingTheFrame_ThenItMatchesTheSnapshot()
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false)
            .Run(["--print-frame", "--size", "120x36", "--mode", "help", "--help-page", "2"]);
        var expected = SnapshotFile.Expect("help-commands-120x36.txt", writer.ToString());

        Assert.Equal(0, exitCode);
        Assert.Equal(Normalize(expected), Normalize(writer.ToString()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(60)]
    public void GivenAdvancedLevel_WhenPrintingTheHelp_ThenItMatchesTheSnapshotThatListsTheAdvancedBindings(int scroll)
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run(
        [
            "--print-frame", "--size", "120x36", "--mode", "help", "--help-page", "1", "--help-scroll",
            scroll.ToString(), "--command-level", "advanced",
        ]);
        var expected = SnapshotFile.Expect($"help-advanced-scroll{scroll}-120x36.txt", writer.ToString());

        Assert.Equal(0, exitCode);
        Assert.Equal(Normalize(expected), Normalize(writer.ToString()));
    }

    [Fact]
    public void GivenAnInvalidCommandLevel_WhenPrintingTheFrame_ThenUsageIsShown()
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run(["--print-frame", "--command-level", "expert"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("--command-level", writer.ToString());
    }

    [Fact]
    public void GivenTheScrolledHelpSnapshots_WhenTogether_ThenEveryPageAndGroupAppears()
    {
        var names = _scrolls.Select(scroll => $"help-all-scroll{scroll}-120x36.txt")
            .Append("help-120x36.txt")
            .Append("help-commands-120x36.txt");
        var text = string.Concat(names.Select(name => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Snapshots",
            name))));

        foreach (var section in _sections)
        {
            Assert.Contains(section, text);
        }
    }

    [Fact]
    public void GivenAnInvalidHelpScroll_WhenPrintingTheFrame_ThenUsageIsShown()
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run(["--print-frame", "--help-scroll", "-1"]);

        Assert.Equal(2, exitCode);
    }

    private static string Normalize(string text)
    {
        return text.ReplaceLineEndings("\n");
    }
}
