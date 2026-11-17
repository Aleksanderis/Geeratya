using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>The styled spans of a single-line prompt: text cut around the caret, the caret mark in between.</summary>
public static class PromptSpans
{
    /// <summary>The prompt in at most <paramref name="width"/> columns: body text, the caret mark in the caret role.</summary>
    public static IReadOnlyList<StyledSpan> Of(PromptText prompt, int width)
    {
        var window = PromptWindow.Of(prompt, width);

        return
        [
            new StyledSpan(window.Before, TextRole.Body),
            new StyledSpan(window.Caret, TextRole.EditCaret),
            new StyledSpan(window.After, TextRole.Body),
        ];
    }
}
