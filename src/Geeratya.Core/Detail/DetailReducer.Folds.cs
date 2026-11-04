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
        var columns = DetailSize.Columns(state);
        var laidOut = Layout(changed, columns, DetailSize.ListRows(state, tab), HeaderRowOf(changed, target, columns));

        if (target.Collapses && target.Kind != DetailTargetKind.Comment && laidOut.Document is { } document)
        {
            // The "Show less" row is gone: the cursor goes to the "more lines" row that replaces it.
            var more = target with { Expands = true, Collapses = false };
            var line = document.Targets.ToList().FindIndex(candidate => candidate == more);
            var viewport = DetailLayout.ViewportRows(DetailSize.ListRows(state, tab));

            laidOut = line < 0 ? laidOut : WithCursor(laidOut, line, viewport);
        }

        return ViewReducer.Replace(state, ReplaceTop(tab, laidOut));
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
