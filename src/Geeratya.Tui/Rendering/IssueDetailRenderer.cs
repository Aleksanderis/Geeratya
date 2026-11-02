using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The body of the active tab while an issue is open on its stack (<c>docs/ux-screens.md</c>, "Issue detail"): a
/// breadcrumb line, then a window of the laid-out document. The reducer lays the document out for the reported
/// width, so drawing only slices it; a document for another width (a size the host has not reported yet) is
/// rebuilt here.
/// </summary>
public sealed class IssueDetailRenderer : IBodyRenderer
{
    private const string Crumb = " › ";
    private const string Separator = " · ";
    private const string LoadingText = "Loading…";
    private const string FailedText = "Some parts failed to load";
    private const string TopText = "Top";
    private const string BottomText = "Bot";
    private const int Percent = 100;
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
        var rows = Math.Max(0, height - DetailLayout.HeaderRows);
        var first = Math.Clamp(top.Scroll, 0, Math.Max(0, document.Lines.Count - rows));
        var shown = Math.Max(0, Math.Min(rows, document.Lines.Count - first));
        var lines = new List<StyledLine>(Math.Min(height, DetailLayout.HeaderRows + shown))
        {
            Breadcrumb(tab, top, first, rows, document.Lines.Count),
        };

        for (var index = first; index < first + shown; index++)
        {
            var line = index == top.Cursor ? CursorLine(document.Lines[index]) : document.Lines[index];

            lines.Add(context.Glyphs == GlyphSet.Ascii ? AsciiBorders(line) : line);
        }

        return lines;
    }

    /// <summary>
    /// Marks the cursor row: a panel edge character (a border, which is always the first span) becomes <c>&gt;</c> in
    /// the selected role; a line without one (frames are dropped on a very narrow screen) is shown selected as a whole.
    /// </summary>
    private static StyledLine CursorLine(StyledLine line)
    {
        if (line.Spans.Count > 0 && line.Spans[0] is { Role: TextRole.Muted, Text.Length: > 0 } first
            && IsBorder(first.Text[0]))
        {
            var marked = new List<StyledSpan>(line.Spans.Count + 1) { new(CursorMark, TextRole.Selected) };

            if (first.Text.Length > 1)
            {
                marked.Add(first with { Text = first.Text[1..] });
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

    private static StyledLine Breadcrumb(TabState tab, DetailState top, int first, int rows, int total)
    {
        var crumb = tab.Detail.Aggregate(tab.Definition.Title, static (text, detail) => $"{text}{Crumb}{detail.Key}");
        var spans = new List<StyledSpan> { new(crumb, TextRole.Heading) };

        if (StateNote(top) is { } note)
        {
            spans.Add(new StyledSpan(Separator, TextRole.Muted));
            spans.Add(note);
        }

        if (total > rows)
        {
            spans.Add(new StyledSpan($"{Separator}{Position(first, rows, total)}", TextRole.Muted));
        }

        return new StyledLine(spans);
    }

    private static StyledSpan? StateNote(DetailState detail)
    {
        if (detail.IssueLoad is Loading || detail.CommentsLoad is Loading || detail.LinksLoad is Loading
            || detail.ChildrenLoad is Loading)
        {
            return new StyledSpan(LoadingText, TextRole.Loading);
        }

        var failed = detail.Issue is not null
            && (detail.IssueLoad is Failed || detail.CommentsLoad is Failed || detail.LinksLoad is Failed
                || detail.ChildrenLoad is Failed);

        return failed ? new StyledSpan(FailedText, TextRole.Error) : null;
    }

    /// <summary>Vim's ruler: Top, Bot or the share scrolled.</summary>
    private static string Position(int first, int rows, int total)
    {
        var last = total - rows;

        if (first <= 0)
        {
            return TopText;
        }

        return first >= last ? BottomText : $"{first * Percent / last}%";
    }
}
