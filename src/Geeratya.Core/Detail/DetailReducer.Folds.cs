using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
    /// <summary>
    /// Shows the rest of a long text (<c>description</c> or <c>comment:ID</c>), or cuts it short again, and lays the
    /// document out again; the state is per issue, so a reload keeps it.
    /// </summary>
    public static AppState ToggleExpanded(AppState state, TabState tab, string key)
    {
        var top = tab.Detail[^1];
        var changed = top with { Expanded = Toggled(top.Expanded, key) };

        return ViewReducer.Replace(state, ReplaceTop(tab, Layout(changed, state.ViewportColumns, tab.VisibleRows)));
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
        var laidOut = Layout(changed, state.ViewportColumns, tab.VisibleRows);
        var moved = ViewReducer.Replace(state, ReplaceTop(tab, laidOut));
        var next = ViewReducer.Find(moved.Session.Tabs, tab.Id)!;

        return JumpTo(moved, next, laidOut.Document!.Sections[section]);
    }

    /// <summary>The section whose top edge is the document line <paramref name="line"/>, -1 for any other line.</summary>
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
