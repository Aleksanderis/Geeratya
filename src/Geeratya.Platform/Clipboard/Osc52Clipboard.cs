using System.Text;

namespace Geeratya.Platform.Clipboard;

/// <summary>
/// Asks the terminal to set the clipboard with the OSC 52 sequence (<c>ESC ] 52 ; c ; base64 BEL</c>). It works over
/// SSH and in many terminals, but the terminal gives no answer: the result says the request was sent, not that the
/// clipboard changed. Some terminals ignore it unless the user allows it.
/// </summary>
public sealed class Osc52Clipboard : IClipboard
{
    /// <summary>Most base64 characters sent; several terminals drop a longer sequence.</summary>
    public const int MaxEncodedCharacters = 100_000;

    private const string Prefix = "\u001b]52;c;";
    private const string Terminator = "\a";

    private readonly TextWriter? _terminal;

    /// <param name="terminal">The writer that reaches the terminal; null when output is not a terminal.</param>
    public Osc52Clipboard(TextWriter? terminal)
    {
        _terminal = terminal;
    }

    public async Task<ClipboardResult> CopyAsync(string text, CancellationToken cancellationToken)
    {
        if (_terminal is null)
        {
            return ClipboardResult.Failure("the output is not a terminal");
        }

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

        if (encoded.Length > MaxEncodedCharacters)
        {
            return ClipboardResult.Failure("the text is too large for the terminal clipboard (OSC 52)");
        }

        await _terminal.WriteAsync($"{Prefix}{encoded}{Terminator}".AsMemory(), cancellationToken);
        await _terminal.FlushAsync(cancellationToken);

        return ClipboardResult.Success("the terminal (OSC 52, it must allow clipboard access)");
    }
}
