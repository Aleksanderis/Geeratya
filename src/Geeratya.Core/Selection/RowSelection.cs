using Geeratya.Core.Backlog;
using Geeratya.Core.Views;

namespace Geeratya.Core.Selection;

/// <summary>
/// The set of rows a write acts on (<c>docs/internal/ux-keymap.md</c>, "Selection and counts"): the marked rows, else the
/// Visual range, else the row under the cursor. Marks and the range are kept as issue keys on the tab, so they
/// survive scrolling and a refresh that moves rows. Pure.
/// </summary>
public static class RowSelection
{
    /// <summary>The Visual range as row indexes, both ends included; null outside Visual mode.</summary>
    public static RowRange? VisualRange(TabState tab)
    {
        if (tab.VisualAnchor is null || tab.Rows.Count == 0)
        {
            return null;
        }

        var cursor = Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1);
        var anchor = IndexOf(tab, tab.VisualAnchor);
        var from = anchor < 0 ? cursor : anchor;

        return new RowRange(Math.Min(from, cursor), Math.Max(from, cursor));
    }

    /// <summary>
    /// How many issues the active set holds: the marks, else the Visual range, else 0 (nothing is selected and a
    /// write acts on the cursor row).
    /// </summary>
    public static int Count(TabState tab)
    {
        if (tab.Marks.Count > 0)
        {
            return tab.Marks.Count;
        }

        return VisualRange(tab) is { } range ? RangeKeys(tab, range).Count : 0;
    }

    /// <summary>The issue keys a write acts on: the marks, else the Visual range, else the cursor row.</summary>
    public static IReadOnlyList<string> Targets(TabState tab)
    {
        if (tab.Marks.Count > 0)
        {
            return InRowOrder(tab);
        }

        if (tab.Rows.Count == 0)
        {
            return [];
        }

        // A sub-task listed under an expanded card is not a row of the tab: the cursor card is the target.
        if (tab.Definition.Layout == ViewLayout.Columns
            && ColumnsNavigation.SelectedIssue(tab) is { IsListed: true } listed)
        {
            return [listed.Key];
        }

        if (VisualRange(tab) is { } range)
        {
            return RangeKeys(tab, range);
        }

        return [tab.Rows[Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1)].Key];
    }

    /// <summary>
    /// The keys in the Visual range. On a Scrum backlog the hidden rows of collapsed sections are left out, so a write
    /// never names an issue the user did not see.
    /// </summary>
    private static IReadOnlyList<string> RangeKeys(TabState tab, RowRange range)
    {
        var rows = tab.Rows.Skip(range.First).Take(range.Length);

        if (BacklogSections.Applies(tab) && tab.CollapsedSections.Count > 0)
        {
            rows = rows.Where(row => !tab.CollapsedSections.Contains(BacklogSections.KeyOf(row)));
        }

        return [.. rows.Select(row => row.Key)];
    }

    /// <summary>True when a single-issue write must refuse: more than the cursor row is selected.</summary>
    public static bool HoldsASet(TabState tab)
    {
        return tab.Marks.Count > 0 || VisualRange(tab) is { Length: > 1 };
    }

    /// <summary>The text of the refusal that a single-issue write gives while a set is active.</summary>
    public static string RefusalText(TabState tab)
    {
        var count = Count(tab);
        var noun = count == 1 ? "issue" : "issues";

        return $"{count} {noun} selected; clear the selection first";
    }

    /// <summary>The tab without the given keys among its marks; also leaves Visual mode when its anchor is gone.</summary>
    public static TabState Without(TabState tab, IReadOnlyList<string> keys)
    {
        var marks = tab.Marks.Where(mark => !Contains(keys, mark)).ToArray();
        var anchorGone = tab.VisualAnchor is { } anchor && Contains(keys, anchor);

        if (marks.Length == tab.Marks.Count && !anchorGone)
        {
            return tab;
        }

        return tab with { Marks = marks, VisualAnchor = anchorGone ? null : tab.VisualAnchor };
    }

    /// <summary>
    /// After the whole result is loaded: marks and the Visual anchor of issues that are no longer in it are dropped.
    /// A partly loaded view keeps them, because the issue may be on a page that is not loaded yet.
    /// </summary>
    public static TabState Pruned(TabState tab)
    {
        // Rows from the cache are the first page only: they say nothing about the whole result.
        if (tab.Next is not null || tab.Truncated || tab.IsStale || (tab.Marks.Count == 0 && tab.VisualAnchor is null))
        {
            return tab;
        }

        var keys = new HashSet<string>(tab.Rows.Select(row => row.Key), StringComparer.OrdinalIgnoreCase);
        var marks = tab.Marks.Where(keys.Contains).ToArray();
        var anchor = tab.VisualAnchor is { } held && keys.Contains(held) ? held : null;

        if (marks.Length == tab.Marks.Count && anchor == tab.VisualAnchor)
        {
            return tab;
        }

        return tab with { Marks = marks, VisualAnchor = anchor };
    }

    private static IReadOnlyList<string> InRowOrder(TabState tab)
    {
        var loaded = tab.Rows.Select(row => row.Key).Where(key => Contains(tab.Marks, key)).ToList();
        var unloaded = tab.Marks.Where(mark => !Contains(loaded, mark));

        return [.. loaded, .. unloaded];
    }

    private static bool Contains(IEnumerable<string> keys, string key)
    {
        return keys.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    private static int IndexOf(TabState tab, string key)
    {
        for (var index = 0; index < tab.Rows.Count; index++)
        {
            if (string.Equals(tab.Rows[index].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
