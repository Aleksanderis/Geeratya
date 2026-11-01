namespace Geeratya.Tui.Tests.Rendering;

public class FrameSnapshotTests
{
    private static readonly string[] _modes = ["normal", "help", "command", "confirm"];
    private static readonly string[] _sizes = ["120x36", "80x24", "40x10"];

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

    private static string Normalize(string text)
    {
        return text.ReplaceLineEndings("\n");
    }
}
