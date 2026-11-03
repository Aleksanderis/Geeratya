using System.Diagnostics;
using Geeratya.Platform.Terminal;

namespace Geeratya.Platform.Tests.Terminal;

[Trait("Os", "Independent")]
public class TerminalAnswerReaderTests
{
    private static readonly char _escape = (char)27;

    [Fact]
    public void GivenColourAnswerThenDa1_WhenReading_ThenTheReadStopsAtDa1AndLeavesNothingBehind()
    {
        var queue = new Queue<char>($"{_escape}]11;rgb:ffff/ffff/ffff\\{_escape}[?62cX");
        var reader = new TerminalAnswerReader(() => queue.Count > 0, queue.Dequeue);

        var text = reader.Read(TimeSpan.FromSeconds(2));

        Assert.True(BackgroundColors.ParseOsc11(text!));
        Assert.Equal(['X'], queue);
    }

    [Fact]
    public void GivenOnlyDa1_WhenReading_ThenTheWaitEndsWithoutAColour()
    {
        var text = Read($"{_escape}[?62c");

        Assert.Null(BackgroundColors.ParseOsc11(text ?? string.Empty));
    }

    [Fact]
    public void GivenSilence_WhenReading_ThenNullComesBackAfterTheTimeout()
    {
        var reader = new TerminalAnswerReader(() => false, () => '\0');

        Assert.Null(reader.Read(TimeSpan.FromMilliseconds(5)));
    }

    [Fact]
    public void GivenSilenceOnALocalTerminal_WhenReading_ThenNoLateWindowIsWaited()
    {
        var reader = new TerminalAnswerReader(() => false, () => '\0');
        var clock = Stopwatch.StartNew();

        var text = ConsoleBackgroundQuery.Read(reader, TimeSpan.FromMilliseconds(5), remote: false);

        Assert.Null(text);
        Assert.True(clock.Elapsed < ConsoleBackgroundQuery.LateWindow);
    }

    [Fact]
    public void GivenARemoteSessionAndASlowReply_WhenReading_ThenTheLateWindowCatchesTheWholeReply()
    {
        var queue = new Queue<char>("]11;rgb:ffff/ffff/ffff\\[?62c");
        var started = DateTime.UtcNow;
        var reader = new TerminalAnswerReader(
            () => DateTime.UtcNow - started > TimeSpan.FromMilliseconds(30) && queue.Count > 0,
            queue.Dequeue);

        var text = ConsoleBackgroundQuery.Read(reader, TimeSpan.FromMilliseconds(5), remote: true);

        Assert.True(reader.Completed);
        Assert.Empty(queue);
        Assert.True(BackgroundColors.ParseOsc11(text!));
    }

    private static string? Read(string input)
    {
        var queue = new Queue<char>(input);
        var reader = new TerminalAnswerReader(() => queue.Count > 0, queue.Dequeue);

        return reader.Read(TimeSpan.FromSeconds(2));
    }
}
