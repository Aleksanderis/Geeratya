using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The body of the active tab while an issue is open on its stack (<c>docs/ux-screens.md</c>, "Issue detail"): one
/// frame whose top edge carries the title of the pane in view, around a window of the laid-out document (the breadcrumb
/// and the ruler are in the status bar, <see cref="DetailStatus"/>). The reducer lays the document out for the reported
/// width, so drawing only slices it; a document for another width (a size the host has not reported yet) is
/// rebuilt here.
/// </summary>
public sealed class IssueDetailRenderer : IBodyRenderer
{
    private const string CursorMark = ">";

    public bool AppliesTo(AppState state)
    {
        return state.Mode != InputMode.Help && ActiveTab(state) is { Detail.Count: > 0 };
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ActiveTab(state)!;
        var top = tab.Detail[^1];
        var document = top.Document is { } laidOut && laidOut.Width == width
            ? laidOut
            : IssueDocumentBuilder.Build(top, width);
        var rows = Math.Max(0, height - DetailLayout.HeaderRows - DetailLayout.FooterRows);
        var first = Math.Clamp(top.Scroll, 0, Math.Max(0, document.Lines.Count - rows));
        var shown = Math.Max(0, Math.Min(rows, document.Lines.Count - first));
        var lines = new List<StyledLine>(height);

        for (var index = first; index < first + shown; index++)
        {
            var line = index == top.Cursor ? CursorLine(document.Lines[index]) : document.Lines[index];

            lines.Add(context.Glyphs == GlyphSet.Ascii ? AsciiBorders(line) : line);
        }

        if (!FrameGrid.CanFrame(width, height) || document.Sections.Count == 0)
        {
            return lines;
        }

        var chips = new List<StyledSpan>();

        if (document.PaneTitles.Count > 0)
        {
            chips.AddRange(document.PaneTitles[document.SectionAt(first)]);
        }

        while (lines.Count < rows)
        {
            lines.Add(StyledLine.Empty);
        }

        lines.Insert(0, PaneFrame.OuterEdge(width, height, true, chips, null));
        lines.Add(PaneFrame.OuterEdge(width, height, false, null, null));

        return lines;
    }

    /// <summary>
    /// Marks the cursor row: the padding cell after the border (the border is always the first span) becomes <c>&gt;</c> in
    /// the selected role; a line without one (frames are dropped on a very narrow screen) is shown selected as a whole.
    /// </summary>
    private static StyledLine CursorLine(StyledLine line)
    {
        if (line.Spans.Count > 0 && line.Spans[0] is { Role: TextRole.Muted, Text.Length: > 1 } first
            && IsBorder(first.Text[0]))
        {
            // The marker goes into the padding column inside the border, never over it.
            var marked = new List<StyledSpan>(line.Spans.Count + 2)
            {
                new(first.Text[..1], TextRole.Muted),
                new(CursorMark, TextRole.Selected),
            };

            if (first.Text.Length > 2)
            {
                marked.Add(first with { Text = first.Text[2..] });
            }

            marked.AddRange(line.Spans.Skip(1));

            return new StyledLine(marked);
        }

        return new StyledLine(line.Spans.Select(static span => span with { Role = TextRole.Selected }).ToArray());
    }

    /// <summary>Swaps the panel border characters for ASCII, one cell for one cell.</summary>
    private static StyledLine AsciiBorders(StyledLine line)
    {
        if (!line.Spans.Any(static span => (span.Role == TextRole.Muted && span.Text.Any(IsBorder))
            || span.Role == TextRole.Editable))
        {
            return line;
        }

        return new StyledLine(line.Spans.Select(static span => span.Role switch
        {
            TextRole.Muted => span with { Text = SwapBorders(span.Text) },
            TextRole.Editable => span with { Text = span.Text.Replace('▾', 'v') },
            _ => span,
        }).ToArray());
    }

    private static string SwapBorders(string text)
    {
        return string.Concat(text.Select(static character => IsBorder(character) ? AsciiOf(character) : character));
    }

    private static bool IsBorder(char character)
    {
        return character is '╭' or '╮' or '╰' or '╯' or '─' or '│';
    }

    private static char AsciiOf(char character)
    {
        return character switch
        {
            '─' => '-',
            '│' => '|',
            _ => '+',
        };
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? state.Session.Tabs.FirstOrDefault(tab => tab.Id == id) : null;
    }
}
