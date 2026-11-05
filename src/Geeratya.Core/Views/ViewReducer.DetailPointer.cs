using Geeratya.Core.Detail;
using Geeratya.Core.State;

namespace Geeratya.Core.Views;

public static partial class ViewReducer
{
    /// <summary>
    /// Whether a plain click at <paramref name="placed"/> also acts on the row of the opened issue it lands on: a
    /// sub-task or other related issue opens, a cut text (<c>… N more lines</c>) expands, and the value of an
    /// editable Details field opens its dropdown. Mouse table, "Detail targets".
    /// </summary>
    public static bool ClickOpensDetailRow(AppState state, PointerPlaced placed)
    {
        return DetailPointerAt(state, placed.Column, placed.Row) is { Tab: { } tab, Hit: { } hit }
            && DetailPointer.OpensOnClick(tab.Detail[^1], hit);
    }

    /// <summary>
    /// Whether the click at <paramref name="placed"/> is on the note of a cut or expanded text of the opened issue:
    /// it expands or cuts it like <c>z a</c>.
    /// </summary>
    public static bool ClickTogglesDetailRow(AppState state, PointerPlaced placed)
    {
        return DetailPointerAt(state, placed.Column, placed.Row) is { Tab: { } tab, Hit: { } hit }
            && DetailPointer.TogglesOnClick(tab.Detail[^1], hit);
    }

    /// <summary>
    /// Whether the click at <paramref name="placed"/> is on the zoom strip or on a pane's frame: it only moves the
    /// focus, so no double-click or right-click action follows it.
    /// </summary>
    public static bool IsDetailFrameClick(AppState state, PointerPlaced placed)
    {
        return DetailPointerAt(state, placed.Column, placed.Row)
            is { Hit.Kind: DetailPointerKind.Strip or DetailPointerKind.Frame };
    }

    /// <summary>
    /// A wheel notch over the opened issue at body cell (<paramref name="column"/>, <paramref name="row"/>): the pane
    /// under the pointer takes the focus first, then the notch's action scrolls it.
    /// </summary>
    public static AppState FocusUnderWheel(AppState state, int column, int row)
    {
        var box = DetailOverlayGeometry.Place(state.ViewportColumns, state.ViewportBodyHeight);

        if (state.Session.Active is not { } id || Find(state.Session.Tabs, id) is not { Detail.Count: > 0 } tab)
        {
            return state;
        }

        return DetailReducer.FocusUnderWheel(state, tab, row - box.Top, column - box.Left);
    }

    private static DetailPointerOver? DetailPointerAt(AppState state, int column, int row)
    {
        if (state.Session.Active is not { } id || Find(state.Session.Tabs, id) is not { Detail.Count: > 0 } tab)
        {
            return null;
        }

        var box = DetailOverlayGeometry.Place(state.ViewportColumns, state.ViewportBodyHeight);

        return new DetailPointerOver(tab, DetailPointer.Locate(state, tab, row - box.Top, column - box.Left));
    }

    private sealed record DetailPointerOver(TabState Tab, DetailPointerHit Hit);
}
