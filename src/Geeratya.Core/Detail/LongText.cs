using Geeratya.Core.Adf;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>
/// Long text in the issue detail (the description, a comment) starts cut short: the first lines, then a
/// <c>… N more lines (Enter)</c> row that shows the rest, and a <c>Show less (Enter)</c> row after it. Pure.
/// </summary>
internal static class LongText
{
    /// <summary>Body rows of the Description pane when it is cut: three text lines and the note.</summary>
    public const int DescriptionRows = 4;

    /// <summary>
    /// Appends <paramref name="lines"/> to <paramref name="content"/>, each opening <paramref name="target"/>;
    /// more than <paramref name="limit"/> lines are cut unless <paramref name="expanded"/>.
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

        var shown = expanded ? lines.Count : limit.Lines - (limit.NoteRow ? 1 : 0);

        var first = expanded ? 0 : Math.Clamp(limit.First, 0, lines.Count - shown);

        for (var index = first; index < first + shown; index++)
        {
            content.Add(lines[index], target);
        }

        if (expanded)
        {
            AddNote(content, "Show less", "collapse", limit.Width, target with { Collapses = true }, false);

            return;
        }

        var hidden = lines.Count - shown;
        var text = hidden == 1 ? "… 1 more line" : $"… {hidden} more lines";

        AddNote(content, text, "expand", limit.Width, target with { Expands = true }, limit.ZoomHint);
    }

    /// <summary>
    /// A muted note with the key that acts on it in the accent: <c>… 11 more lines   Enter expand</c>.
    /// </summary>
    private static void AddNote(
        PanelContent content,
        string text,
        string action,
        int width,
        DetailTarget target,
        bool zoomHint)
    {
        var spans = new List<StyledSpan>
        {
            new(text, TextRole.Muted),
            new("   ", TextRole.Muted),
            new("Enter", TextRole.Key),
            new($" {action}", TextRole.Muted),
        };

        if (zoomHint)
        {
            spans.Add(new StyledSpan("   ", TextRole.Muted));
            spans.Add(new StyledSpan("<space>z", TextRole.Key));
            spans.Add(new StyledSpan(" zoom", TextRole.Muted));
        }

        var line = new StyledLine(spans);

        if (DisplayWidth.Of(line.PlainText) <= width)
        {
            content.Add(line, target);

            return;
        }

        var wrapped = new WrappedText(width, false);

        wrapped.Append(line.PlainText, TextRole.Muted);
        content.Finish(wrapped, target);
    }
}
