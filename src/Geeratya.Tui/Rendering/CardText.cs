namespace Geeratya.Tui.Rendering;

/// <summary>
/// Text of a board card: the summary wrapped over two lines. Pure; widths go through <see cref="TextWidth"/>.
/// </summary>
public static class CardText
{
    private const string Ellipsis = "…";

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

        var fitted = TextWidth.Fit(text, width);

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
        if (TextWidth.Of(text) <= width)
        {
            return text;
        }

        if (width <= 1)
        {
            return TextWidth.Fit(text, width);
        }

        return $"{TextWidth.Fit(text, width - 1)}{Ellipsis}";
    }
}
