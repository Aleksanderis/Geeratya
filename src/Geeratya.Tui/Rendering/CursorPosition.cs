using System.Globalization;
using Geeratya.Core.Backlog;
using Geeratya.Core.Views;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Where the cursor stands, for the status bar: <c>5/40</c> in a list or backlog, <c>col 2/4  card 3/7</c> on a
/// board. Counts follow the loaded rows, so a plus sign follows the total while more pages exist.
/// </summary>
public static class CursorPosition
{
    private const string CardWord = "card";
    private const string ColumnWord = "col";
    private const string MorePages = "+";

    /// <summary>The position text, empty when there is no row to stand on (nothing loaded, an empty sprint section).</summary>
    public static string Text(TabState tab)
    {
        var more = tab.Next is not null || tab.Truncated ? MorePages : string.Empty;

        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return BoardText(tab, more);
        }

        if (tab.Rows.Count == 0
            || tab.FocusedSprintId is not null
            || (BacklogSections.Applies(tab) && BacklogCursor.OnCollapsedHeader(tab)))
        {
            return string.Empty;
        }

        var row = Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1) + 1;

        return string.Create(CultureInfo.InvariantCulture, $"{row}/{tab.Rows.Count}{more}");
    }

    private static string BoardText(TabState tab, string more)
    {
        if (ColumnsNavigation.SelectedColumn(tab) is not { } selection)
        {
            return string.Empty;
        }

        var cards = selection.Rows.Count;
        var card = cards == 0 ? 0 : Math.Clamp(selection.Index, 0, cards - 1) + 1;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ColumnWord} {selection.Column + 1}/{selection.ColumnCount}  {CardWord} {card}/{cards}{more}");
    }
}
