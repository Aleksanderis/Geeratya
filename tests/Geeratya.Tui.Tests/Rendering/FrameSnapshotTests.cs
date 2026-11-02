namespace Geeratya.Tui.Tests.Rendering;

public class FrameSnapshotTests
{
    private static readonly string[] _modes = ["normal", "help", "command", "confirm", "insert"];
    private static readonly string[] _sizes = ["120x36", "80x24", "40x10"];

    private static readonly string[] _sections =
        ["Normal mode", "Help mode", "Command mode", "Confirm mode", "Insert mode", "Palette commands", ":quit"];

    private static readonly int[] _scrolls = [0, 20, 40, 70];

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
        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", $"{mode}-{size}.txt"));

        var exitCode = new ApplicationRunner(writer, false).Run(["--print-frame", "--size", size, "--mode", mode]);

        Assert.Equal(0, exitCode);
        Assert.Equal(Normalize(expected), Normalize(writer.ToString()));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(70)]
    public void GivenHelpScrolled_WhenPrintingTheFrame_ThenItMatchesTheSnapshotThatShowsTheLaterSections(int scroll)
    {
        using var writer = new StringWriter();
        var path = Path.Combine(AppContext.BaseDirectory, "Snapshots", $"help-scroll{scroll}-120x36.txt");
        var expected = File.ReadAllText(path);

        var exitCode = new ApplicationRunner(writer, false)
            .Run(["--print-frame", "--size", "120x36", "--mode", "help", "--help-scroll", scroll.ToString()]);

        Assert.Equal(0, exitCode);
        Assert.Equal(Normalize(expected), Normalize(writer.ToString()));
    }

    [Fact]
    public void GivenTheScrolledHelpSnapshots_WhenTogether_ThenEveryModeSectionAppears()
    {
        var text = string.Concat(_scrolls.Select(scroll => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Snapshots",
            scroll == 0 ? "help-120x36.txt" : $"help-scroll{scroll}-120x36.txt"))));

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
