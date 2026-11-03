namespace Geeratya.Platform.Terminal;

/// <summary>
/// The real query on the console: writes <c>ESC ] 11 ; ? ST</c> followed by DA1 and reads the answer with
/// <see cref="TerminalAnswerReader"/>. Only called where <see cref="TerminalBackgroundDetector"/> allows it (never on
/// Windows).
/// </summary>
public sealed class ConsoleBackgroundQuery : ITerminalBackgroundQuery
{
    private const string Query = "\u001b]11;?\u001b\\\u001b[c";

    public string? Ask(TimeSpan timeout)
    {
        try
        {
            // Reading the keyboard state first makes .NET put the tty in no-echo mode: a reply that arrives before
            // that is echoed onto the screen as ^[]11;rgb:... instead of being read.
            _ = Console.KeyAvailable;
            Console.Out.Write(Query);
            Console.Out.Flush();

            return new TerminalAnswerReader(() => Console.KeyAvailable, () => Console.ReadKey(intercept: true).KeyChar)
                .Read(timeout);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlatformNotSupportedException)
        {
            // A best-effort probe: startup goes on with the COLORFGBG fallback.
            return null;
        }
    }
}
