namespace Geeratya.Platform.Clipboard;

/// <summary>Tries each clipboard in order and stops at the first that accepts the text.</summary>
public sealed class ChainedClipboard : IClipboard
{
    private readonly IReadOnlyList<IClipboard> _clipboards;

    public ChainedClipboard(IReadOnlyList<IClipboard> clipboards)
    {
        _clipboards = clipboards;
    }

    public async Task<ClipboardResult> CopyAsync(string text, CancellationToken cancellationToken)
    {
        var messages = new List<string>();

        foreach (var clipboard in _clipboards)
        {
            var result = await clipboard.CopyAsync(text, cancellationToken);

            if (result.Copied)
            {
                return result;
            }

            messages.Add(result.Message);
        }

        return ClipboardResult.Failure(messages.Count == 0 ? "no clipboard available" : string.Join("; ", messages));
    }
}
