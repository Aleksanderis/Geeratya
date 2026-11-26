using Geeratya.Core.Clipboard;
using Geeratya.Core.State;

namespace Geeratya.Core.Editing;

/// <summary>
/// The clipboard side of <c>editor.copy</c> and <c>editor.cut</c>, shared by every text surface that selects: the
/// effect that puts the selected text on the clipboard. Pure.
/// </summary>
public static class SelectionCopy
{
    private const string TextDescription = "the selected text";

    /// <summary>The effect that copies <paramref name="selected"/>, or none when nothing is selected.</summary>
    public static IReadOnlyList<Effect> Effects(string selected)
    {
        if (selected.Length == 0)
        {
            return [];
        }

        var lines = selected.Count(static character => character == '\n') + 1;

        return [new CopyToClipboardEffect(selected, lines == 1 ? TextDescription : $"{lines} lines")];
    }
}
