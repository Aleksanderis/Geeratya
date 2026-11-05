using System.Text;
using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Detail;

/// <summary>
/// The plain text behind the lines of an issue detail, for copying: the frame is dropped, wrapped fields join into one
/// line, and a whole Description or comment is taken from its source rather than from the cut lines on screen. Pure.
/// </summary>
public static class DetailText
{
    private const int UnwrappedWidth = 10_000;
    private const string LineBreak = "\n";
    private const char Space = ' ';

    /// <summary>The text of a line without the frame and the padding after it.</summary>
    public static string Of(StyledLine line)
    {
        return string.Concat(line.Spans.Where(static span => span.Role != TextRole.Border).Select(static span => span.Text))
            .TrimEnd();
    }

    /// <summary>The pane row of <paramref name="line"/> in the main column of the shown document, null on a frame edge.</summary>
    public static YankAnchor? AnchorAt(DetailState detail, int line)
    {
        if (detail.Document is not { } document || line < 0 || line >= document.Lines.Count)
        {
            return null;
        }

        var section = document.SectionAt(line);
        var first = document.Sections[section];

        if (line < first || line >= ContentEnd(document, section))
        {
            return null;
        }

        return new YankAnchor(document.SectionIds[section], Offset(detail, document, section) + line - first);
    }

    /// <summary>
    /// The text of the rows from <paramref name="anchor"/> to the cursor line, inside the pane of the anchor: a cursor
    /// in another pane stops at the edge of the anchor's pane on that side. The lines are as shown (the pane's own
    /// wrapping); the lines of one wrapped field join with a space.
    /// </summary>
    public static string Selected(DetailState detail, YankAnchor anchor)
    {
        if (detail.Document is not { } shown)
        {
            return string.Empty;
        }

        var full = IssueDocumentBuilder.Build(detail, shown.Width);
        var section = full.SectionIds.ToList().IndexOf(anchor.Pane);

        if (section < 0)
        {
            return string.Empty;
        }

        var first = full.Sections[section];
        var count = ContentEnd(full, section) - first;
        var cursor = CursorRow(detail, shown, anchor, full, section);
        var low = Math.Clamp(Math.Min(anchor.Row, cursor), 0, Math.Max(0, count - 1));
        var high = Math.Clamp(Math.Max(anchor.Row, cursor), 0, Math.Max(0, count - 1));

        return count == 0 ? string.Empty : Joined(full, first + low, first + high);
    }

    /// <summary>
    /// The lines of the shown document the selection covers (the part of it that is on screen), null without a
    /// selection or when its pane is not shown. The renderer draws them in the selection tint.
    /// </summary>
    public static LineBlock? SelectedLines(DetailState detail)
    {
        if (detail.YankAnchor is not { } anchor || detail.Document is not { } shown || detail.SideCursor is not null)
        {
            return null;
        }

        var section = shown.SectionIds.ToList().IndexOf(anchor.Pane);

        if (section < 0)
        {
            return null;
        }

        var first = shown.Sections[section];
        var last = ContentEnd(shown, section) - 1;
        var cursorSection = shown.SectionAt(detail.Cursor);
        var cursor = detail.Cursor;

        if (cursorSection != section)
        {
            cursor = cursorSection < section ? first : last;
        }

        var start = Math.Clamp(first + anchor.Row - Offset(detail, shown, section), first, Math.Max(first, last));
        var low = Math.Min(start, cursor);
        var high = Math.Max(start, cursor);

        return new LineBlock(low, high + 1);
    }

    /// <summary>
    /// The whole field or comment under the cursor, also the part cut off on screen; the text of the cursor line (its
    /// whole wrapped field) elsewhere. Empty when the cursor rests on nothing.
    /// </summary>
    public static string Field(DetailState detail)
    {
        var target = DetailReducer.TargetOf(detail);

        if (target is { Kind: DetailTargetKind.Field, Value: DetailFieldIds.Description }
            && detail.Issue?.Description is { } description)
        {
            return Unwrapped(description);
        }

        if (target is { Kind: DetailTargetKind.Comment } && !target.Note
            && detail.Comments.FirstOrDefault(comment => comment.Id == target.Value) is { } comment)
        {
            return Unwrapped(comment.Body);
        }

        return BlockAtCursor(detail);
    }

    private static string Unwrapped(AdfDocument document)
    {
        var lines = AdfRenderer.Render(document, UnwrappedWidth).Lines.Select(Of).ToList();

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join(LineBreak, lines);
    }

    private static string BlockAtCursor(DetailState detail)
    {
        var onSide = detail.SideCursor is not null && detail.Document?.Side is not null;
        var document = onSide ? detail.Document!.Side! : detail.Document;
        var cursor = onSide ? detail.SideCursor!.Value : detail.Cursor;

        if (document is null || cursor < 0 || cursor >= document.Lines.Count)
        {
            return string.Empty;
        }

        var block = document.BlockAt(cursor);

        return block is null ? Of(document.Lines[cursor]) : Joined(document, block.Start, block.End - 1);
    }

    // The cursor's row in the pane of the anchor; a cursor in another pane is held at that pane's edge.
    private static int CursorRow(DetailState detail, DetailDocument shown, YankAnchor anchor, DetailDocument full, int section)
    {
        var cursorSection = shown.SectionAt(detail.Cursor);

        if (shown.SectionIds[cursorSection] == anchor.Pane)
        {
            return AnchorAt(detail, detail.Cursor)?.Row ?? anchor.Row;
        }

        var before = cursorSection < shown.SectionIds.ToList().IndexOf(anchor.Pane);

        return before ? 0 : ContentEnd(full, section) - full.Sections[section] - 1;
    }

    private static string Joined(DetailDocument document, int first, int last)
    {
        var lines = new List<string>();
        var builder = new StringBuilder();

        for (var line = first; line <= last; line++)
        {
            var text = Of(document.Lines[line]);
            var joins = line > first && document.BlockAt(line) is { } block && block == document.BlockAt(line - 1);

            if (joins)
            {
                builder.Append(Space).Append(text.TrimStart());

                continue;
            }

            if (line > first)
            {
                lines.Add(builder.ToString());
                builder.Clear();
            }

            builder.Append(text);
        }

        lines.Add(builder.ToString());

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join(LineBreak, lines);
    }

    // One past the last content line of the pane: its bottom edge (or the next pane's top edge) ends it.
    private static int ContentEnd(DetailDocument document, int section)
    {
        var end = section + 1 < document.PaneStarts.Count ? document.PaneStarts[section + 1] : document.Lines.Count;

        if (end > document.Sections[section] && IsEdge(document.Lines[end - 1]))
        {
            end--;
        }

        return end;
    }

    private static bool IsEdge(StyledLine line)
    {
        return line.Spans.Count > 0 && line.Spans.All(static span => span.Role == TextRole.Border);
    }

    // The rows scrolled out of a pane that scrolls inside itself (Comments, a zoomed pane); the other panes show
    // from their first row.
    private static int Offset(DetailState detail, DetailDocument document, int section)
    {
        var scrolls = document.PaneScrolls[section] is not null
            && (document.SectionIds[section] == PaneIds.Comments || detail.Zoomed is not null);

        return scrolls ? document.PaneScrolls[section]!.ClampedOffset : 0;
    }
}
