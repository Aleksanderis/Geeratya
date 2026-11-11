namespace Geeratya.Platform.Clipboard;

/// <summary>
/// The Win32 clipboard calls the native clipboard needs, as a seam: the real one calls user32 and kernel32
/// (<see cref="Win32ClipboardApi"/>), tests script the sequence on any system.
/// </summary>
public interface IWindowsClipboardApi
{
    /// <summary>The Win32 error code of the last failed call, for the message.</summary>
    int LastError { get; }

    /// <summary>Opens the clipboard (<c>OpenClipboard</c>); false when another program holds it.</summary>
    bool Open();

    /// <summary>Empties it and takes ownership (<c>EmptyClipboard</c>); the clipboard must be open.</summary>
    bool Empty();

    /// <summary>Stores the text as <c>CF_UNICODETEXT</c>; the clipboard must be open and empty.</summary>
    bool SetUnicodeText(string text);

    /// <summary>Closes the clipboard (<c>CloseClipboard</c>).</summary>
    void Close();
}
