using Geeratya.Core.State;

namespace Geeratya.Core.Clipboard;

/// <summary>Turns the outcome of a copy into the status line. Pure.</summary>
public static class ClipboardReducer
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Finished(AppState state, ClipboardCopied copied)
    {
        if (!copied.Copied)
        {
            return new ReduceResult(StatusMessages.Warn(state, $"Cannot copy: {copied.Message}"), _noEffects);
        }

        return new ReduceResult(state with { Message = $"Copied {copied.Description} ({copied.Message})" }, _noEffects);
    }
}
