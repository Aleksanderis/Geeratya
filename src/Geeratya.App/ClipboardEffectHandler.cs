using Geeratya.Core.Clipboard;
using Geeratya.Core.State;
using Geeratya.Platform.Clipboard;

namespace Geeratya.App;

/// <summary>Runs <see cref="CopyToClipboardEffect"/> through the platform clipboard and reports the outcome.</summary>
public sealed class ClipboardEffectHandler : ILocalEffectHandler
{
    private readonly IClipboard _clipboard;

    public ClipboardEffectHandler(IClipboard clipboard)
    {
        _clipboard = clipboard;
    }

    public bool CanRun(Effect effect)
    {
        return effect is CopyToClipboardEffect;
    }

    /// <summary>The command that reports the outcome, or null when the effect is not a copy effect.</summary>
    public async Task<Command?> RunAsync(Effect effect, CancellationToken cancellationToken)
    {
        if (effect is not CopyToClipboardEffect copy)
        {
            return null;
        }

        try
        {
            var result = await _clipboard.CopyAsync(copy.Text, cancellationToken);

            return new ClipboardCopied(copy.Description, result.Copied, result.Message);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            var reason = $"the clipboard could not be used ({exception.GetType().Name})";

            return new ClipboardCopied(copy.Description, false, reason);
        }
    }
}
