using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>The styled spans of a single-line prompt: text cut around the caret, the caret mark in between.</summary>
public static class PromptSpans
{
    /// <summary>The prompt in at most <paramref name="width"/> columns, the caret mark in <paramref name="caretRole"/>.</summary>
    public static IReadOnlyList<StyledSpan> Of(PromptText prompt, int width, TextRole caretRole)
    {
        var window = PromptWindow.Of(prompt, width);

        return
        [
            new StyledSpan(window.Before, TextRole.Body),
            new StyledSpan(window.Caret, caretRole),
            new StyledSpan(window.After, TextRole.Body),
        ];
    }
}
