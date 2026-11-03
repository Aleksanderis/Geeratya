using System.Diagnostics;
using System.Text;

namespace Geeratya.Platform.Terminal;

/// <summary>
/// Reads the answer to an OSC 11 query that was followed by a DA1 query (<c>ESC [ c</c>). Every terminal answers DA1,
/// and answers come in order, so the DA1 reply marks the end: a terminal that ignores OSC 11 is detected as soon as it
/// answers DA1 instead of by the timeout, and no late OSC answer is left to reach the UI as keystrokes.
/// </summary>
/// <param name="keyAvailable">Whether a key can be read without blocking.</param>
/// <param name="readKey">Reads the next key as a character.</param>
public sealed class TerminalAnswerReader(Func<bool> keyAvailable, Func<char> readKey)
{
    private const char Bell = '\u0007';
    private const char Backslash = '\\';
    private const char DeviceAttributesEnd = 'c';
    private const string ColourAnswerMarker = "]11";
    private const int MaxLength = 256;

    /// <summary>
    /// Reads until the DA1 reply or the timeout. Returns what came before the DA1 reply, null when nothing came. The
    /// string terminator <c>ESC \</c> reaches <c>Console.ReadKey</c> as a bare backslash (the escape is swallowed), so
    /// both it and BEL end the colour answer.
    /// </summary>
    public string? Read(TimeSpan timeout)
    {
        var builder = new StringBuilder();
        var clock = Stopwatch.StartNew();
        var colourAnswerEnded = false;

        while (clock.Elapsed < timeout && builder.Length < MaxLength)
        {
            if (!keyAvailable())
            {
                Thread.Sleep(1);

                continue;
            }

            var character = readKey();

            if (colourAnswerEnded && character == DeviceAttributesEnd)
            {
                break;
            }

            if (!colourAnswerEnded && character is Bell or Backslash)
            {
                colourAnswerEnded = true;
            }
            else if (!colourAnswerEnded && character == DeviceAttributesEnd && !ContainsColourAnswer(builder))
            {
                // Only DA1 came back: this terminal does not know OSC 11.
                break;
            }

            builder.Append(character);
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    private static bool ContainsColourAnswer(StringBuilder builder)
    {
        return builder.ToString().Contains(ColourAnswerMarker, StringComparison.Ordinal);
    }
}
