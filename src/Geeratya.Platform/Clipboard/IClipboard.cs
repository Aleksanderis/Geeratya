namespace Geeratya.Platform.Clipboard;

/// <summary>Puts text on the system clipboard; a seam so tests never touch the real one.</summary>
public interface IClipboard
{
    /// <summary>
    /// Copies the text. Never throws for a missing tool or an unsupported system: the result carries the message
    /// for the status line.
    /// </summary>
    Task<ClipboardResult> CopyAsync(string text, CancellationToken cancellationToken);
}
