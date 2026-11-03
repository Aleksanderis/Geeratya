using System.Globalization;
using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Core.Views;

/// <summary>The text a tab shows in the tab bar: a source prefix and the source name, one line, cut by width.</summary>
public static class TabTitles
{
    private const string Ellipsis = "…";

    /// <summary>The full title of a view, for example <c>JQL: project = GCM</c>; control characters become spaces.</summary>
    public static string For(ViewDefinition definition)
    {
        var prefix = definition.Source switch
        {
            ViewSourceKind.Board => "Board",
            ViewSourceKind.Backlog or ViewSourceKind.ScrumBacklog => "Backlog",
            ViewSourceKind.Sprint => "Sprint",
            ViewSourceKind.SavedFilter => "Filter",
            _ => "JQL",
        };

        return OneLine($"{prefix}: {definition.Title}");
    }

    /// <summary>
    /// Cuts <paramref name="text"/> to at most <paramref name="width"/> columns, ending in an ellipsis when it was
    /// cut; a wide character is never split.
    /// </summary>
    public static string Cut(string text, int width)
    {
        if (DisplayWidth.Of(text) <= width)
        {
            return text;
        }

        if (width <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var used = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);

        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            var elementWidth = DisplayWidth.OfCluster(element);

            if (used + elementWidth > width - 1)
            {
                break;
            }

            builder.Append(element);
            used += elementWidth;
        }

        return $"{builder}{Ellipsis}";
    }

    private static string OneLine(string text)
    {
        return text.Any(char.IsControl) ? string.Concat(text.Select(ch => char.IsControl(ch) ? ' ' : ch)) : text;
    }
}
