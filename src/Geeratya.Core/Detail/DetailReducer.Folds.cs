using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
    /// <summary>
    /// Shows the rest of a long text (<c>description</c> or <c>comment:ID</c>), or cuts it short again, and lays the
    /// document out again; the state is per issue, so a reload keeps it.
    /// </summary>
    public static AppState ToggleExpanded(AppState state, TabState tab, DetailTarget target)
    {
        var top = tab.Detail[^1];
        var changed = top with { Expanded = Toggled(top.Expanded, target.ExpandKey) };
        var laidOut = Layout(changed, DetailSize.Columns(state), DetailSize.ListRows(state, tab));

        if (target.Collapses && laidOut.Document is { } document)
        {
            // The "Show less" row is gone: the cursor goes to the "more lines" row that replaces it.
            var more = target with { Expands = true, Collapses = false };
            var line = document.Targets.ToList().FindIndex(candidate => candidate == more);
            var viewport = DetailLayout.ViewportRows(DetailSize.ListRows(state, tab));

            laidOut = line < 0 ? laidOut : WithCursor(laidOut, line, viewport);
        }

        return ViewReducer.Replace(state, ReplaceTop(tab, laidOut));
    }

    /// <summary>
    /// Folds section <paramref name="section"/> to a preview, or opens it again; the cursor goes to the start of the
    /// section. The first section (the fields) is never folded: null.
    /// </summary>
    public static AppState? ToggleFolded(AppState state, TabState tab, int section)
    {
        var top = tab.Detail[^1];

        if (section <= 0 || top.Document is not { } document || section >= document.SectionIds.Count)
        {
            return null;
        }

        var changed = top with { Folded = Toggled(top.Folded, document.SectionIds[section]) };
        var laidOut = Layout(changed, DetailSize.Columns(state), DetailSize.ListRows(state, tab));
        var moved = ViewReducer.Replace(state, ReplaceTop(tab, laidOut));
        var next = ViewReducer.Find(moved.Session.Tabs, tab.Id)!;

        return JumpTo(moved, next, laidOut.Document!.Sections[section]);
    }

    /// <summary>The section whose top edge is document line <paramref name="line"/>, -1 for any other line.</summary>
    private static int EdgeSection(DetailDocument document, int line)
    {
        for (var section = 1; section < document.Sections.Count; section++)
        {
            if (document.Sections[section] - 1 == line)
            {
                return section;
            }
        }

        return -1;
    }

    private static List<string> Toggled(IReadOnlyList<string> keys, string key)
    {
        var next = keys.ToList();

        if (!next.Remove(key))
        {
            next.Add(key);
        }

        return next;
    }
}
