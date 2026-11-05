namespace Geeratya.Platform.Clipboard;

/// <summary>Whether the text was handed to a clipboard; <see cref="Message"/> says how, or why not.</summary>
/// <param name="Copied">True when a clipboard accepted the text.</param>
/// <param name="Message">How it was copied (the tool), or why it could not be; never the copied text.</param>
public sealed record ClipboardResult(bool Copied, string Message)
{
    public static ClipboardResult Success(string via)
    {
        return new ClipboardResult(true, via);
    }

    public static ClipboardResult Failure(string message)
    {
        return new ClipboardResult(false, message);
    }
}
