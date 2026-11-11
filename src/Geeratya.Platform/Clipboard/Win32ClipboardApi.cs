using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Geeratya.Platform.Clipboard;

/// <summary>The real Win32 clipboard: user32 and kernel32 through <c>LibraryImport</c> (Native AOT safe).</summary>
[SupportedOSPlatform("windows")]
public sealed partial class Win32ClipboardApi : IWindowsClipboardApi
{
    private const uint UnicodeText = 13;
    private const uint MoveableMemory = 0x0002;

    public int LastError { get; private set; }

    public bool Open()
    {
        // A NULL owner would make SetClipboardData fail after EmptyClipboard, so open with a real window: the
        // console window (a hidden pseudo window under Windows Terminal), else the foreground one.
        var owner = GetConsoleWindow();

        if (owner == IntPtr.Zero)
        {
            owner = GetForegroundWindow();
        }

        return Remember(OpenClipboard(owner));
    }

    public bool Empty()
    {
        return Remember(EmptyClipboard());
    }

    public void Close()
    {
        CloseClipboard();
    }

    public bool SetUnicodeText(string text)
    {
        // CF_UNICODETEXT is UTF-16 with a terminating zero character.
        var byteCount = (text.Length + 1) * sizeof(char);
        var memory = GlobalAlloc(MoveableMemory, (UIntPtr)byteCount);

        if (memory == IntPtr.Zero)
        {
            return Remember(false);
        }

        var target = GlobalLock(memory);

        if (target == IntPtr.Zero)
        {
            Remember(false);
            GlobalFree(memory);

            return false;
        }

        try
        {
            Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
            Marshal.WriteInt16(target, text.Length * sizeof(char), 0);
        }
        finally
        {
            GlobalUnlock(memory);
        }

        if (SetClipboardData(UnicodeText, memory) == IntPtr.Zero)
        {
            Remember(false);
            GlobalFree(memory);

            return false;
        }

        // The system owns the memory now.
        return true;
    }

    private bool Remember(bool succeeded)
    {
        if (!succeeded)
        {
            LastError = Marshal.GetLastPInvokeError();
        }

        return succeeded;
    }

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetConsoleWindow();

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(IntPtr owner);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr SetClipboardData(uint format, IntPtr memory);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalLock(IntPtr memory);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(IntPtr memory);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalFree(IntPtr memory);
}
