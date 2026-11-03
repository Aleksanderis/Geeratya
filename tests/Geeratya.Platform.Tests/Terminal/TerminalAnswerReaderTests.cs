using Geeratya.Platform.Terminal;

namespace Geeratya.Platform.Tests.Terminal;

public class TerminalAnswerReaderTests
{
    [Fact]
    public void GivenColourAnswerThenDa1_WhenReading_ThenTheReadStopsAtDa1AndLeavesNothingBehind()
    {
        var queue = new Queue<char>("\u001b]11;rgb:ffff/ffff/ffff\\\u001b[?62cX");
        var reader = new TerminalAnswerReader(() => queue.Count > 0, queue.Dequeue);

        var text = reader.Read(TimeSpan.FromSeconds(2));

        Assert.True(BackgroundColors.ParseOsc11(text!));
        Assert.Equal(['X'], queue);
    }

    [Fact]
    public void GivenOnlyDa1_WhenReading_ThenTheWaitEndsWithoutAColour()
    {
        var text = Read("\u001b[?62c");

        Assert.Null(BackgroundColors.ParseOsc11(text ?? string.Empty));
    }

    [Fact]
    public void GivenSilence_WhenReading_ThenNullComesBackAfterTheTimeout()
    {
        var reader = new TerminalAnswerReader(() => false, () => '\0');

        Assert.Null(reader.Read(TimeSpan.FromMilliseconds(5)));
    }

    private static string? Read(string input)
    {
        var queue = new Queue<char>(input);
        var reader = new TerminalAnswerReader(() => queue.Count > 0, queue.Dequeue);

        return reader.Read(TimeSpan.FromSeconds(2));
    }
}
