using Geeratya.Platform.Clipboard;

namespace Geeratya.App.Tests.Fakes;

/// <summary>Records the copied texts and returns a canned result, or throws when asked to.</summary>
public sealed class FakeClipboard : IClipboard
{
    private readonly ClipboardResult _result;
    private readonly Exception? _failure;

    public FakeClipboard(ClipboardResult result, Exception? failure = null)
    {
        _result = result;
        _failure = failure;
    }

    public List<string> Copied { get; } = [];

    public Task<ClipboardResult> CopyAsync(string text, CancellationToken cancellationToken)
    {
        Copied.Add(text);

        return _failure is null ? Task.FromResult(_result) : Task.FromException<ClipboardResult>(_failure);
    }
}
