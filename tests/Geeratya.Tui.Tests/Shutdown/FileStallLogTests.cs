using Geeratya.App.Tests.Fakes;
using Geeratya.Tui.Shutdown;

namespace Geeratya.Tui.Tests.Shutdown;

public sealed class FileStallLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"geeratya-stalls-{Guid.NewGuid():N}");

    [Fact]
    public void GivenAStall_WhenReported_ThenALineWithTheLengthAndLastWorkIsAppended()
    {
        var log = new FileStallLog(_folder, new ManualTimeProvider());

        log.Stalled(TimeSpan.FromSeconds(5.5), "KeyPressed");
        log.Recovered(TimeSpan.FromSeconds(9));

        var lines = File.ReadAllLines(Path.Combine(_folder, FileStallLog.FileName));

        Assert.Equal(2, lines.Length);
        Assert.Contains("stalled for 5.5 s; last work started: KeyPressed", lines[0]);
        Assert.StartsWith("2026-11-02T12:00:00", lines[0]);
        Assert.Contains("running again after about 9.0 s", lines[1]);
    }

    [Fact]
    public void GivenAnUnwritableFolder_WhenReporting_ThenNothingThrows()
    {
        Directory.CreateDirectory(_folder);

        var blocker = Path.Combine(_folder, "file");

        File.WriteAllText(blocker, "x");

        var log = new FileStallLog(Path.Combine(blocker, "inside"), new ManualTimeProvider());

        log.Stalled(TimeSpan.FromSeconds(6), "Tick");
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
