using Geeratya.Core.Adf;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>
/// Long text in the issue detail (the description, a comment) starts cut short: the first lines, then a
/// <c>… N more lines (Enter)</c> row that shows the rest, and a <c>Show less (Enter)</c> row after it. Pure.
/// </summary>
internal static class LongText
{
    /// <summary>Description lines shown before the rest is cut.</summary>
    public const int DescriptionLines = 8;

    /// <summary>Lines of one comment (the byline included) shown before the rest is cut.</summary>
    public const int CommentLines = 6;

    /// <summary>
    /// Appends <paramref name="lines"/> to <paramref name="content"/>, each opening <paramref name="target"/>; more than
    /// <paramref name="limit"/> lines are cut unless <paramref name="expanded"/>.
    /// </summary>
    public static void Add(
        PanelContent content,
        IReadOnlyList<StyledLine> lines,
        DetailTarget? target,
        bool expanded,
        LongTextLimit limit)
    {
        if (lines.Count <= limit.Lines || target is null)
        {
            foreach (var line in lines)
            {
                content.Add(line, target);
            }

            return;
        }

        var shown = expanded ? lines.Count : limit.Lines;

        for (var index = 0; index < shown; index++)
        {
            content.Add(lines[index], target);
        }

        if (expanded)
        {
            AddNote(content, "Show less (Enter)", limit.Width, target with { Collapses = true });

            return;
        }

        var hidden = lines.Count - limit.Lines;
        var text = hidden == 1 ? "… 1 more line (Enter)" : $"… {hidden} more lines (Enter)";

        AddNote(content, text, limit.Width, target with { Expands = true });
    }

    private static void AddNote(PanelContent content, string text, int width, DetailTarget target)
    {
        var wrapped = new WrappedText(width, false);

        wrapped.Append(text, TextRole.Link);
        content.Finish(wrapped, target);
    }
}
