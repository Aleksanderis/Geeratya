using Geeratya.Core.Detail;
using Geeratya.Core.Views;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The status bar text of an open issue: where it was reached from (the breadcrumb), what is loading or failed, and
/// vim's ruler of the document (Top, Bot or the share scrolled).
/// </summary>
public static class DetailStatus
{
    private const string Crumb = " › ";
    private const string Separator = " · ";
    private const string LoadingText = "Loading…";
    private const string FailedText = "Some parts failed to load";
    private const string TopText = "Top";
    private const string BottomText = "Bot";
    private const int Percent = 100;

    /// <summary>
    /// The text for a status bar with <paramref name="room"/> cells to spare, for an overlay of
    /// <paramref name="listRows"/> list rows (<see cref="DetailSize"/>): the full breadcrumb when it fits, else
    /// without the name of the view it was reached from (the issue keys, the note and the ruler matter most).
    /// </summary>
    public static string Text(TabState tab, int room, int listRows)
    {
        var full = Text(tab, true, listRows);

        return TextWidth.Of(full) <= room ? full : Text(tab, false, listRows);
    }

    private static string Text(TabState tab, bool withSource, int listRows)
    {
        var top = tab.Detail[^1];
        var keys = string.Join(Crumb, tab.Detail.Select(static detail => detail.Key));
        var crumb = withSource ? $"{tab.Definition.Title}{Crumb}{keys}" : keys;
        var note = Note(top);
        var rows = DetailLayout.ViewportRows(listRows);
        var total = top.Document?.Lines.Count ?? 0;
        var parts = new List<string> { crumb };

        if (note.Length > 0)
        {
            parts.Add(note);
        }

        if (total > rows)
        {
            var first = Math.Clamp(top.Scroll, 0, Math.Max(0, total - rows));

            parts.Add(Position(first, rows, total));
        }

        return string.Join(Separator, parts);
    }

    private static string Note(DetailState detail)
    {
        if (detail.IssueLoad is Loading || detail.CommentsLoad is Loading || detail.LinksLoad is Loading
            || detail.ChildrenLoad is Loading)
        {
            return LoadingText;
        }

        var failed = detail.Issue is not null
            && (detail.IssueLoad is Failed || detail.CommentsLoad is Failed || detail.LinksLoad is Failed
                || detail.ChildrenLoad is Failed);

        return failed ? FailedText : string.Empty;
    }

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
