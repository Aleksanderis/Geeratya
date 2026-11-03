namespace Geeratya.Platform.Terminal;

/// <summary>
/// The real query on the console: writes <c>ESC ] 11 ; ? ST</c> followed by DA1 and reads the answer with
/// <see cref="TerminalAnswerReader"/>. Only called where <see cref="TerminalBackgroundDetector"/> allows it (never
/// on Windows). A reply that is late would reach the UI as keystrokes, so when the first window ends without the DA1
/// reply and something already came (or the session is over SSH), the read waits a second, longer window.
/// </summary>
public sealed class ConsoleBackgroundQuery(EnvironmentReader environment) : ITerminalBackgroundQuery
{
    /// <summary>The extra wait for a reply that is under way or on a slow link.</summary>
    public static readonly TimeSpan LateWindow = TimeSpan.FromMilliseconds(250);

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

            var reader = new TerminalAnswerReader(
                () => Console.KeyAvailable,
                () => Console.ReadKey(intercept: true).KeyChar);

            return Read(reader, timeout, IsRemote());
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or PlatformNotSupportedException)
        {
            // A best-effort probe: startup goes on with the COLORFGBG fallback.
            return null;
        }
    }

    /// <summary>
    /// The first window, then the late window when no DA1 reply came and something is under way or the session is
    /// remote.
    /// </summary>
    public static string? Read(TerminalAnswerReader reader, TimeSpan timeout, bool remote)
    {
        var text = reader.Read(timeout);

        if (!reader.Completed && (text is not null || remote))
        {
            text = reader.Read(LateWindow);
        }

        return text;
    }

    private bool IsRemote()
    {
        return environment.Get("SSH_CONNECTION") is not null || environment.Get("SSH_TTY") is not null;
    }
}
