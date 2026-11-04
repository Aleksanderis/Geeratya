using Geeratya.Core.Lists;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Text;

/// <summary>
/// Text of a board card: the summary wrapped over two lines. Pure; widths go through <see cref="DisplayWidth"/>.
/// </summary>
public static class CardText
{
    private const string Ellipsis = "…";
    private const string ParentMark = "↑";
    private const string SelectedMark = ">";
    private const string MarkedMark = "*";
    private const string Blank = " ";

    /// <summary>
    /// The one-line form of a card in the columns layout: cursor mark, tree branch, type marker, set mark, key, parent
    /// and summary, cut to <paramref name="width"/>. The first cell is the cursor mark, so a flash label can take it.
    /// </summary>
    public static string Compact(IssueSummary issue, bool selected, bool marked, bool nested, bool more, int width)
    {
        var mark = selected ? SelectedMark : Blank;
        var marker = IssueTypeMarker.For(issue.IssueType);
        var branch = nested ? (more ? "├" : "└") : string.Empty;
        var parent = !nested && issue.Parent is { } owner ? $" {ParentMark}{owner.Key}" : string.Empty;
        var star = marked ? MarkedMark : Blank;

        return Shorten($"{mark}{branch}{marker}{star}{issue.Key}{parent} {issue.Summary}", width);
    }

    /// <summary>
    /// The summary over two lines of at most <paramref name="width"/> columns. The first line breaks at the last
    /// space that fits (a word longer than the line is cut); the second ends with an ellipsis when it does not fit.
    /// </summary>
    public static CardLines TwoLines(string summary, int width)
    {
        var text = summary.Trim();

        if (width <= 0 || text.Length == 0)
        {
            return new CardLines(string.Empty, string.Empty);
        }

        var fitted = DisplayWidth.Fit(text, width);

        if (fitted.Length == text.Length)
        {
            return new CardLines(text, string.Empty);
        }

        var space = fitted.LastIndexOf(' ');
        var cut = space > 0 ? space : fitted.Length;
        var rest = text[cut..].TrimStart();

        return new CardLines(text[..cut].TrimEnd(), Shorten(rest, width));
    }

    /// <summary>
    /// <paramref name="text"/> cut to <paramref name="width"/> columns, ending with an ellipsis when it was cut.
    /// </summary>
    public static string Shorten(string text, int width)
    {
        if (DisplayWidth.Of(text) <= width)
        {
            return text;
        }

        if (width <= 1)
        {
            return DisplayWidth.Fit(text, width);
        }

        return $"{DisplayWidth.Fit(text, width - 1)}{Ellipsis}";
    }
}
