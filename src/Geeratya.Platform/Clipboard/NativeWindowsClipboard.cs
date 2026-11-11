namespace Geeratya.Platform.Clipboard;

/// <summary>
/// Copies on Windows through the Win32 clipboard (open, empty, set <c>CF_UNICODETEXT</c>, close): no process start,
/// no length cap, the text never appears on a command line. Another program may hold the clipboard for a moment, so
/// opening is retried a few times. Failure is a result, never an exception; the chain then tries the next clipboard.
/// </summary>
public sealed class NativeWindowsClipboard : IClipboard
{
    public const string Name = "Windows clipboard";

    private const int OpenAttempts = 5;
    private static readonly TimeSpan _openDelay = TimeSpan.FromMilliseconds(20);

    private readonly IWindowsClipboardApi _api;

    public NativeWindowsClipboard(IWindowsClipboardApi api)
    {
        _api = api;
    }

    public async Task<ClipboardResult> CopyAsync(string text, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= OpenAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_api.Open())
            {
                return Store(text);
            }

            if (attempt < OpenAttempts)
            {
                await Task.Delay(_openDelay, cancellationToken);
            }
        }

        return ClipboardResult.Failure($"the Windows clipboard is busy (error {_api.LastError})");
    }

    private ClipboardResult Store(string text)
    {
        try
        {
            if (!_api.Empty())
            {
                return ClipboardResult.Failure($"the Windows clipboard could not be emptied (error {_api.LastError})");
            }

            if (!_api.SetUnicodeText(text))
            {
                return ClipboardResult.Failure($"the Windows clipboard refused the text (error {_api.LastError})");
            }

            return ClipboardResult.Success(Name);
        }
        finally
        {
            _api.Close();
        }
    }
}
