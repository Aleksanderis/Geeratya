using System.Globalization;

namespace Geeratya.Core.Text;

/// <summary>
/// A prompt cut to a width around its caret: the text before the caret, the caret mark (<c>_</c> after the last
/// character, <c>|</c> between two characters) and the text after it. Wide characters are never split. A cut side
/// starts (before) or ends (after) with an ellipsis. The mark is for single-line surfaces that draw it as text (status
/// bar, filters); the form box and the editors draw the caret as a styled cell instead, see <see cref="CaretRows"/>.
/// </summary>
/// <param name="Before">The visible text before the caret.</param>
/// <param name="Caret">The caret mark.</param>
/// <param name="After">The visible text after the caret.</param>
/// <param name="TailCut">Whether <paramref name="After"/> ends with an ellipsis standing for hidden text.</param>
public sealed record PromptWindow(string Before, string Caret, string After, bool TailCut = false)
{
    public const string EndMark = "_";
    public const string InsideMark = "|";

    // How much of the room goes to the text after the caret when both sides are too long.
    private const int AfterShare = 3;
    private const int MaxPasses = 4;
    private const string Ellipsis = "…";
    private const int EllipsisWidth = 1;
    private const int MinimumRoomForEllipsis = 4;

    /// <summary>The prompt cut so that text and caret mark fit in <paramref name="width"/> columns (at least the mark).</summary>
    public static PromptWindow Of(PromptText prompt, int width)
    {
        var caret = prompt.Caret;
        var before = Clusters(prompt.Text[..caret]);
        var after = Clusters(prompt.Text[caret..]);
        var mark = caret == prompt.Text.Length ? EndMark : InsideMark;
        var room = Math.Max(0, width - mark.Length);

        if (Width(before) + Width(after) <= room)
        {
            return new PromptWindow(string.Concat(before), mark, string.Concat(after));
        }

        var marked = room >= MinimumRoomForEllipsis;
        var cutLeft = false;
        var cutRight = false;
        List<string> shownBefore = [];
        List<string> shownAfter = [];

        // Each cut side costs a column for its ellipsis, which can cut the other side too: settle in a few passes.
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var left = cutLeft ? EllipsisWidth : 0;
            var right = cutRight ? EllipsisWidth : 0;
            var available = Math.Max(0, room - left - right);
            var afterRoom = Math.Min(Width(after), available / AfterShare);
            var beforeRoom = Math.Min(Width(before), available - afterRoom);

            afterRoom = available - beforeRoom;
            shownBefore = TakeEnd(before, beforeRoom);
            shownAfter = TakeStart(after, afterRoom);
            cutLeft = marked && shownBefore.Count < before.Count;
            cutRight = marked && shownAfter.Count < after.Count;
        }

        var head = cutLeft ? Ellipsis : string.Empty;
        var tail = cutRight ? Ellipsis : string.Empty;

        return new PromptWindow($"{head}{string.Concat(shownBefore)}", mark, $"{string.Concat(shownAfter)}{tail}", cutRight);
    }

    private static List<string> Clusters(string text)
    {
        var clusters = new List<string>();
        var elements = StringInfo.GetTextElementEnumerator(text);

        while (elements.MoveNext())
        {
            clusters.Add(elements.GetTextElement());
        }

        return clusters;
    }

    private static int Width(List<string> clusters)
    {
        return clusters.Sum(DisplayWidth.OfCluster);
    }

    private static List<string> TakeStart(List<string> clusters, int room)
    {
        var taken = new List<string>();
        var used = 0;

        foreach (var cluster in clusters)
        {
            used += DisplayWidth.OfCluster(cluster);

            if (used > room)
            {
                break;
            }

            taken.Add(cluster);
        }

        return taken;
    }

    private static List<string> TakeEnd(List<string> clusters, int room)
    {
        var taken = new List<string>();
        var used = 0;

        for (var index = clusters.Count - 1; index >= 0; index--)
        {
            used += DisplayWidth.OfCluster(clusters[index]);

            if (used > room)
            {
                break;
            }

            taken.Insert(0, clusters[index]);
        }

        return taken;
    }
}
