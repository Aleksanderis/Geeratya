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
            lines.Add(context.Glyphs == GlyphSet.Ascii ? AsciiBorders(document.Lines[index]) : document.Lines[index]);
        }

        return lines;
    }

    /// <summary>Swaps the panel border characters for ASCII, one cell for one cell.</summary>
    private static StyledLine AsciiBorders(StyledLine line)
    {
        if (!line.Spans.Any(static span => span.Role == TextRole.Muted && span.Text.Any(IsBorder)))
        {
            return line;
        }

        return new StyledLine(line.Spans.Select(static span => span.Role == TextRole.Muted
            ? span with { Text = SwapBorders(span.Text) }
            : span).ToArray());
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
        if (detail.IssueLoad is Loading || detail.CommentsLoad is Loading || detail.LinksLoad is Loading)
        {
            return new StyledSpan(LoadingText, TextRole.Loading);
        }

        var failed = detail.Issue is not null
            && (detail.IssueLoad is Failed || detail.CommentsLoad is Failed || detail.LinksLoad is Failed);

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
