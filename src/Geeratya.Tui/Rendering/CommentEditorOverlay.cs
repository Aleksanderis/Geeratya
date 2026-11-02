using Geeratya.Core.Comments;
using Geeratya.Core.Editing;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The comment editor box (<c>docs/ux-screens.md</c>, "Comment editor"): a centred box over the issue with the text
/// in rows, the cursor drawn as a selected cell, a line for what the user should know (formatting warning, error,
/// saving) and a key footer. Pure. The box shrinks to the text and scrolls so that the cursor row stays visible.
/// </summary>
public static class CommentEditorOverlay
{
    private const string NewTitle = "Comment on";
    private const string EditTitle = "Edit comment on";
    private const string SavingText = "Saving…";
    private const string PlaceholderText = " Type the comment here";
    private const string SearchingText = "searching…";
    private const string LookupFailedText = "lookup failed";
    private const string NoMatchText = "No match";
    private const int PopupRows = 5;
    private const int MinimumBoxWidth = 12;
    private const int MinimumHeight = 6;
    private const int MinimumTextRows = 4;
    private const int FooterHeight = 2;
    private const int MaxNoteRows = 3;
    private const int TopGap = 1;
    private const int WallColumns = 4;
    private const int DividerMargin = 2;

    /// <summary>Draws the editor of <paramref name="state"/> over <paramref name="body"/> (exactly <paramref name="height"/> lines).</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        KeymapTable keymap,
        int width,
        int height)
    {
        var boxWidth = CommentEditor.BoxWidth(width);

        if (state.Editor is not { } editor || boxWidth < MinimumBoxWidth || height < MinimumHeight)
        {
            return body;
        }

        var inner = boxWidth - WallColumns;
        var notes = new List<StyledLine>([.. PopupLines(editor, inner), .. Notes(editor, inner)]);
        var footer = FooterText(keymap, editor);
        var rows = EditorLayout.Rows(editor.Buffer.Text, EditorLayout.Capacity(inner));
        var area = Math.Clamp(height - 2 - notes.Count - FooterHeight, 1, Math.Max(MinimumTextRows, rows.Count));
        var box = BoxLines(editor, rows, notes, footer, boxWidth, area);
        var top = Math.Min(TopGap, Math.Max(0, height - box.Count));
        var left = (width - boxWidth) / 2;
        var result = body.ToList();

        for (var index = 0; index < box.Count && top + index < result.Count; index++)
        {
            result[top + index] = OverlayBox.Place(box[index], left, width);
        }

        return result;
    }

    private static List<StyledLine> BoxLines(
        CommentEditor editor,
        IReadOnlyList<EditorRow> rows,
        List<StyledLine> notes,
        string footer,
        int boxWidth,
        int area)
    {
        var contentWidth = boxWidth - 2;
        var title = editor.IsEdit ? EditTitle : NewTitle;
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, $"{title} {editor.Key}") };

        lines.AddRange(TextLines(editor, rows, area).Select(line => OverlayBox.Wall(line, contentWidth)));
        lines.AddRange(notes.Select(note => OverlayBox.Wall(note, contentWidth)));
        lines.Add(OverlayBox.Wall(Divider(contentWidth), contentWidth));
        lines.Add(OverlayBox.Wall(StyledLine.Of(footer, TextRole.Muted), contentWidth));
        lines.Add(OverlayBox.Bottom(boxWidth));

        return lines;
    }

    /// <summary>The visible rows of the text, the cursor row last when the text is longer than the box.</summary>
    private static List<StyledLine> TextLines(CommentEditor editor, IReadOnlyList<EditorRow> rows, int area)
    {
        var text = editor.Buffer.Text;
        var cursor = EditorLayout.Locate(text, rows, editor.Buffer.Cursor);
        var first = cursor.Row < area ? 0 : cursor.Row - area + 1;
        var lines = new List<StyledLine>();

        for (var index = first; index < rows.Count && lines.Count < area; index++)
        {
            var row = rows[index];

            lines.Add(index == cursor.Row ? CursorRow(editor, text, row) : Plain(text, row));
        }

        while (lines.Count < area)
        {
            lines.Add(StyledLine.Empty);
        }

        return lines;
    }

    private static StyledLine Divider(int contentWidth)
    {
        return StyledLine.Of(new string(OverlayBox.Horizontal, Math.Max(0, contentWidth - DividerMargin)), TextRole.Muted);
    }

    private static StyledLine Plain(string text, EditorRow row)
    {
        return StyledLine.Of(text[row.Start..row.End]);
    }

    private static StyledLine CursorRow(CommentEditor editor, string text, EditorRow row)
    {
        var index = Math.Clamp(editor.Buffer.Cursor, row.Start, row.End);
        var end = index < row.End ? EditorLayout.NextStart(text, index) : index;
        var under = index < row.End ? text[index..end] : " ";
        var spans = new List<StyledSpan>
        {
            new(text[row.Start..index], TextRole.Body),
            new(under, TextRole.Selected),
            new(text[end..row.End], TextRole.Body),
        };

        if (text.Length == 0)
        {
            spans.Add(new StyledSpan(PlaceholderText, TextRole.Muted));
        }

        return new StyledLine(spans);
    }

    /// <summary>The lines under the text: saving, the error, the warning; at most a few rows, cut with an ellipsis.</summary>
    private static List<StyledLine> Notes(CommentEditor editor, int inner)
    {
        var lines = new List<StyledLine>();

        if (editor.IsSaving)
        {
            lines.Add(StyledLine.Of(SavingText, TextRole.Loading));
        }

        AddWrapped(lines, editor.Error, TextRole.Error, inner);
        AddWrapped(lines, editor.Warning, TextRole.Stale, inner);
        AddWrapped(lines, UnseenText(editor), TextRole.Stale, inner);

        return lines.Count <= MaxNoteRows ? lines : [.. lines.Take(MaxNoteRows)];
    }

    // People already mentioned in the opened comment who did not show up among those who can see the issue.
    private static string? UnseenText(CommentEditor editor)
    {
        var people = editor.UnseenNow;

        if (people.Count == 0)
        {
            return null;
        }

        var names = string.Join(", ", people.Select(person => person.Name));

        return $"May not see {editor.Key}, so the mention may do nothing: {names}.";
    }

    /// <summary>The suggestion rows under the text (the chosen row marked with <c>&gt;</c>) and a line for the lookup.</summary>
    private static List<StyledLine> PopupLines(CommentEditor editor, int inner)
    {
        var lines = new List<StyledLine>();

        if (editor.Suggest is not { } popup)
        {
            return lines;
        }

        var width = Math.Max(1, inner - 2);
        var cursor = Math.Clamp(popup.Cursor, 0, Math.Max(0, popup.Items.Count - 1));
        var first = Math.Clamp(cursor - PopupRows + 1, 0, Math.Max(0, popup.Items.Count - PopupRows));
        var shown = Math.Min(PopupRows, Math.Max(0, popup.Items.Count - first));

        foreach (var index in Enumerable.Range(first, shown))
        {
            var highlighted = index == cursor;
            var pointer = highlighted ? ">" : " ";
            var text = TextWidth.Fit(SuggestionText.Row(popup.Items[index]), width);

            lines.Add(StyledLine.Of($"{pointer} {text}", highlighted ? TextRole.Selected : TextRole.Body));
        }

        if (PopupNotice(popup, inner) is { } notice)
        {
            lines.Add(notice);
        }

        return lines;
    }

    private static StyledLine? PopupNotice(SuggestionState popup, int inner)
    {
        return popup.Status switch
        {
            SuggestionStatus.Loading => StyledLine.Of(SearchingText, TextRole.Loading),
            SuggestionStatus.Failed => StyledLine.Of(
                TextWidth.Fit($"{LookupFailedText}: {popup.Error}", inner),
                TextRole.Error),
            SuggestionStatus.Loaded when popup.Items.Count == 0 => StyledLine.Of(NoMatchText, TextRole.Muted),
            _ => null,
        };
    }

    private static void AddWrapped(List<StyledLine> lines, string? text, TextRole role, int inner)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // The editor's own character wrap works for any text, so a note wraps the same way.
        foreach (var row in EditorLayout.Rows(text, inner))
        {
            lines.Add(StyledLine.Of(text[row.Start..row.End], role));
        }
    }

    private static string PopupFooterText(KeymapTable keymap)
    {
        var down = keymap.FindKeys(InputMode.Insert, ActionIds.PickerNext);
        var up = keymap.FindKeys(InputMode.Insert, ActionIds.PickerPrevious);
        var accept = keymap.FindKeys(InputMode.Insert, ActionIds.FormFieldNext);
        var enter = keymap.FindKeys(InputMode.Insert, ActionIds.FormNext);
        var close = keymap.FindKeys(InputMode.Insert, ActionIds.Cancel);

        return $"{down}/{up} choose   {accept}/{enter} accept   {close} close list";
    }

    private static string FooterText(KeymapTable keymap, CommentEditor editor)
    {
        var save = keymap.FindKeys(InputMode.Insert, ActionIds.FormSubmit);
        var cancel = keymap.FindKeys(InputMode.Insert, ActionIds.Cancel);
        var newLine = keymap.FindKeys(InputMode.Insert, ActionIds.FormNext);
        var verb = editor.IsEdit ? "save" : "send";

        if (editor.Suggest is { Items.Count: > 0 })
        {
            return PopupFooterText(keymap);
        }

        return $"{save} {verb}   {newLine} new line   {cancel} cancel";
    }
}
