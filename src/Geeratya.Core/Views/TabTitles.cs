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
        return OneLine($"{Kind(definition)}: {definition.Title}");
    }

    /// <summary>The label of a tab chip, <c>JQL project = GCM</c>: a Jira noun and the source name, no colon.</summary>
    public static string Label(ViewDefinition definition)
    {
        return OneLine($"{Kind(definition)} {definition.Title}");
    }

    /// <summary>The label of a tab chip: its own name when it has one, else <see cref="Label(ViewDefinition)"/>.</summary>
    public static string Label(TabState tab)
    {
        return tab.Name is { Length: > 0 } name ? OneLine(name) : Label(tab.Definition);
    }

    /// <summary>
    /// The suffix that tells a tab from an earlier one with the same label: empty for the first, <c> (2)</c> for the
    /// second, and so on.
    /// </summary>
    public static string Qualifier(IReadOnlyList<TabState> tabs, int index)
    {
        var label = Label(tabs[index]);
        var earlier = 0;

        for (var other = 0; other < index; other++)
        {
            earlier += Label(tabs[other]) == label ? 1 : 0;
        }

        return earlier == 0 ? string.Empty : $" ({earlier + 1})";
    }

    /// <summary>The label with its qualifier, as the tab list shows it.</summary>
    public static string Qualified(IReadOnlyList<TabState> tabs, int index)
    {
        return $"{Label(tabs[index])}{Qualifier(tabs, index)}";
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

    private static string Kind(ViewDefinition definition)
    {
        return definition.Source switch
        {
            ViewSourceKind.Board => "Board",
            ViewSourceKind.Backlog or ViewSourceKind.ScrumBacklog => "Backlog",
            ViewSourceKind.Sprint => "Sprint",
            ViewSourceKind.SavedFilter => "Filter",
            _ => "JQL",
        };
    }

    private static string OneLine(string text)
    {
        return text.Any(char.IsControl) ? string.Concat(text.Select(ch => char.IsControl(ch) ? ' ' : ch)) : text;
    }
}
