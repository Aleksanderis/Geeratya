using Geeratya.Core.Keymap;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The note lines of a body that has no rows to show: loading, empty, failed and not-found. One look for all of
/// them (<c>docs/ux-look.md</c>, "Content is padded by one column inside each pane"): a padding column, left
/// aligned at the top of the pane; muted for the plain notes, the loading and error roles for the network states.
/// </summary>
public static class StateBody
{
    private const string Pad = " ";
    private const string FallbackRetry = "the palette (refresh)";

    public static StyledLine Loading(Glyphs glyphs)
    {
        return StyledLine.Of($"{Pad}Loading{glyphs.Ellipsis}", TextRole.Loading);
    }

    /// <summary>An empty source, with an optional second muted line (the JQL, a hint).</summary>
    public static IEnumerable<StyledLine> Empty(string text, string? detail = null)
    {
        yield return StyledLine.Of($"{Pad}{text}", TextRole.Muted);

        if (!string.IsNullOrEmpty(detail))
        {
            yield return StyledLine.Of($"{Pad}{detail}", TextRole.Muted);
        }
    }

    /// <summary>The reason in the error role, then the retry key from the keymap.</summary>
    public static IEnumerable<StyledLine> Failure(string message, KeymapTable keymap)
    {
        yield return StyledLine.Of($"{Pad}{message}", TextRole.Error);
        yield return StyledLine.Of($"{Pad}{RetryText(keymap)}", TextRole.Muted);
    }

    public static string RetryText(KeymapTable keymap)
    {
        return $"{keymap.FindKeyLabel(InputMode.Normal, ActionIds.ViewRefresh) ?? FallbackRetry} to retry";
    }
}
