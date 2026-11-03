using System.Diagnostics;
using System.Text;

namespace Geeratya.Platform.Terminal;

/// <summary>
/// Reads the answer to an OSC 11 query that was followed by a DA1 query (<c>ESC [ c</c>). Every terminal answers DA1,
/// and answers come in order, so the DA1 reply marks the end: a terminal that ignores OSC 11 is detected as soon as it
/// answers DA1 instead of by the timeout, and no late OSC answer is left to reach the UI as keystrokes. A read that ran
/// out of time can be continued with another call (the text read so far is kept).
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

    private readonly StringBuilder _builder = new();
    private bool _colourAnswerEnded;

    /// <summary>True once the DA1 reply was seen: nothing more is coming from the query.</summary>
    public bool Completed { get; private set; }

    /// <summary>
    /// Reads until the DA1 reply or the timeout. Returns what came before the DA1 reply, null when nothing came. The
    /// string terminator <c>ESC \</c> reaches <c>Console.ReadKey</c> as a bare backslash (the escape is swallowed), so
    /// both it and BEL end the colour answer.
    /// </summary>
    public string? Read(TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();

        while (!Completed && clock.Elapsed < timeout && _builder.Length < MaxLength)
        {
            if (!keyAvailable())
            {
                Thread.Sleep(1);

                continue;
            }

            Accept(readKey());
        }

        return _builder.Length > 0 ? _builder.ToString() : null;
    }

    private void Accept(char character)
    {
        if (_colourAnswerEnded && character == DeviceAttributesEnd)
        {
            Completed = true;

            return;
        }

        if (!_colourAnswerEnded && character is Bell or Backslash)
        {
            _colourAnswerEnded = true;
        }
        else if (!_colourAnswerEnded && character == DeviceAttributesEnd && !ContainsColourAnswer())
        {
            // Only DA1 came back: this terminal does not know OSC 11.
            Completed = true;

            return;
        }

        _builder.Append(character);
    }

    private bool ContainsColourAnswer()
    {
        return _builder.ToString().Contains(ColourAnswerMarker, StringComparison.Ordinal);
    }
}
