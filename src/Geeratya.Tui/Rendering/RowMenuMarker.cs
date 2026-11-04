using System.Text;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The <c>...</c> marker in the last cells of the selected list row, backlog row, board card (its first text line,
/// the summary) and the cursor line of an opened issue (<c>docs/ux-mouse.md</c>, <c>RowMenuButton</c>). It is text, so
/// it shows in every colour tier and in ASCII. A click on it opens the actions menu without moving the cursor, a second mouse path to
/// <c>&lt;space&gt;a</c> for terminals whose multiplexer swallows the right-click. The cells it covers are reported
/// back so the hit test uses what was drawn.
/// </summary>
public static class RowMenuMarker
{
    /// <summary>The marker text; three plain cells.</summary>
    public const string Text = "...";

    /// <summary>The selected part of the row must be at least this wide, else the row is too narrow and shows none.</summary>
    public const int MinRowWidth = 24;

    private const int KeyLookahead = 2;

    /// <summary>
    /// Replaces the last cells of the selected line of <paramref name="body"/> with the marker and returns its cells
    /// (body rows), or null when no marker is drawn: not in Normal mode, an overlay or special screen covers the body,
    /// the cursor is not on an issue (a sprint header), or the row is narrower than <see cref="MinRowWidth"/>. <paramref name="limit"/> is the first column the marker
    /// may not cover (the right frame of the wide detail overlay), so it never reaches the view around the box.
    /// </summary>
    public static OverlayRegion? Stamp(List<StyledLine> body, AppState state, int? limit = null)
    {
        if (!Applies(state, out var key))
        {
            return null;
        }

        for (var row = 0; row < body.Count; row++)
        {
            if (Run(body[row], NearKey(body, row, key), key is null) is not { } run)
            {
                continue;
            }

            var end = Math.Min(run.End, limit ?? run.End);

            if (end - run.Start < MinRowWidth)
            {
                return null;
            }

            var start = end - Text.Length;
            body[row] = Replace(body[row], start, end);

            return new OverlayRegion(MouseTarget.RowMenuButton, row, start, Text.Length, 1, 0);
        }

        return null;
    }

    // key stays null for an opened issue (the cursor line has no key to look for).
    private static bool Applies(AppState state, out string? key)
    {
        key = null;

        if (state.Mode != InputMode.Normal || state.Picker is not null || state.Jump is not null
            || state.Editor is not null || state.ProfileList is not null || state.SettingsScreen is not null
            || state.ContextMenu is not null || state.Form is not null)
        {
            return false;
        }

        if (state.Session.Active is not { } id
            || state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == id) is not { } tab)
        {
            return false;
        }

        if (tab.Detail.Count > 0)
        {
            return true;
        }

        if (tab.Selected < 0 || tab.Selected >= tab.Rows.Count || tab.FocusedSprintId is not null)
        {
            return false;
        }

        // The columns layout keeps its cursor card in SelectedKey; the list index does not follow it there.
        var columns = tab.Definition.Layout == ViewLayout.Columns;

        key = columns && tab.SelectedKey is { } card ? card : tab.Rows[tab.Selected].Key;

        return true;
    }

    // The key is on the row, or on one of the two rows under it that also hold selected cells: a board card shows its
    // key on the last of its three text rows and the marker sits on the first, so it does not cover the priority
    // glyph and the assignee chip. The key matches as a whole word, so A-1 is not found in A-10.
    private static bool NearKey(List<StyledLine> body, int row, string? key)
    {
        if (key is null || HasKey(body[row], key))
        {
            return true;
        }

        var last = Math.Min(body.Count - 1, row + KeyLookahead);

        for (var index = row + 1; index <= last; index++)
        {
            if (HasKey(body[index], key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasKey(StyledLine line, string key)
    {
        var text = line.PlainText;
        var at = text.IndexOf(key, StringComparison.Ordinal);

        while (at >= 0)
        {
            var end = at + key.Length;

            if (end == text.Length || !char.IsLetterOrDigit(text[end]))
            {
                return true;
            }

            at = text.IndexOf(key, end, StringComparison.Ordinal);
        }

        return false;
    }

    // The cells of the selected-styled spans of a line, null when it has none or (outside the detail) it does not show
    // the key of the selected issue, which keeps section headers and column titles from taking the marker.
    private static Cells? Run(StyledLine line, bool keyNear, bool noKey)
    {
        var column = 0;
        int? start = null;
        var end = 0;
        var inner = 0;
        var tail = -1;

        foreach (var span in line.Spans)
        {
            var width = TextWidth.Of(span.Text);

            if (IsSelected(span))
            {
                start ??= column;
                end = column + width;
            }

            if (tail < 0 && span.Ground.HasFlag(SpanGround.Tail))
            {
                tail = column;
            }

            column += width;

            // Where the content ends: before the closing border of a framed pane.
            inner = span.Role is TextRole.Border or TextRole.FrameFocus ? inner : column;
        }

        // The cursor line of an opened issue marks only its first cell; the row runs on to the border.
        end = noKey && start is not null ? Math.Max(end, inner) : end;

        // The right-aligned text of a bar (the time of a comment) stays visible: the marker ends one cell before it.
        end = tail > 0 ? Math.Min(end, tail - 1) : end;

        if (start is null || !keyNear)
        {
            return null;
        }

        return new Cells(start.Value, end);
    }

    private static bool IsSelected(StyledSpan span)
    {
        return span.Role == TextRole.Selected || (span.Ground & (SpanGround.Selection | SpanGround.Cursor)) != 0;
    }

    // The line with the cells [start, end) drawn as the marker, in the style of the span that holds the first of them;
    // a wide glyph cut by the edge becomes spaces.
    private static StyledLine Replace(StyledLine line, int start, int end)
    {
        var spans = new List<StyledSpan>(line.Spans.Count);
        var column = 0;
        var placed = false;

        foreach (var span in line.Spans)
        {
            var text = new StringBuilder();

            foreach (var rune in span.Text.EnumerateRunes())
            {
                var from = column;
                var to = column + TextWidth.Of(rune);

                column = to;

                if (to <= start || from >= end)
                {
                    text.Append(rune.ToString());
                    continue;
                }

                if (from < start)
                {
                    text.Append(' ', start - from);
                }

                if (!placed)
                {
                    text.Append(Text);
                    placed = true;
                }

                if (to > end)
                {
                    text.Append(' ', to - end);
                }
            }

            spans.Add(span with { Text = text.ToString() });
        }

        return new StyledLine(spans);
    }

    private readonly record struct Cells(int Start, int End);
}
