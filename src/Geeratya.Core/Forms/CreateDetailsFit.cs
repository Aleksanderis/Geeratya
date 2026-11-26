using Geeratya.Core.Detail;
using Geeratya.Core.State;

namespace Geeratya.Core.Forms;

/// <summary>
/// How many rows the Details pane of the create form has in the overlay of a viewport, and how many of its optional
/// fields fit in them (the rest folds behind <c>Ctrl-f</c>). The row numbers of the stacked layout are the ones
/// <c>CreatePaneLayout</c> places the panes with. Pure.
/// </summary>
public static class CreateDetailsFit
{
    /// <summary>
    /// Overlay rows below the title pane in one column from which Description and Details share them.
    /// </summary>
    public const int StackedMinimum = 11;

    /// <summary>Overlay rows below the title pane in one column from which "Not editable here" shows too.</summary>
    public const int StackedNotEditableMinimum = 16;

    /// <summary>The fewest rows of the Description pane when it shares the column with Details.</summary>
    public const int DescriptionShortest = 5;

    /// <summary>The most rows of the Description pane when it shares the column with Details.</summary>
    public const int DescriptionTallest = 8;

    // A field block is a label row and a value row, with a blank row between two blocks.
    private const int BlockRows = 2;
    private const int TitleRows = 1;
    private const int NotEditableText = 1;

    /// <summary>
    /// The rows of the Details pane for a viewport; 0 while the viewport is unknown. An estimate: it assumes a one-row
    /// title pane and a Description pane in one column (the pane scrolls when it is off by a row or two).
    /// </summary>
    public static int Rows(FormState form, int columns, int bodyHeight)
    {
        if (columns <= 0 || bodyHeight < 0)
        {
            return 0;
        }

        var box = DetailOverlayGeometry.Place(columns, bodyHeight);

        if (DetailLayout.HasSidebar(box.Width))
        {
            return Math.Max(0, box.Height - Panel.EdgeRows);
        }

        var rest = box.Height - TitleRows - Panel.EdgeRows;
        var notEditable = form.Fields.Any(static field => !field.IsEditable) ? NotEditableText + 1 : NotEditableText;
        var notHeight = notEditable + Panel.EdgeRows;

        if (rest - notHeight >= StackedNotEditableMinimum)
        {
            rest -= notHeight;
        }

        if (rest < StackedMinimum)
        {
            return Math.Max(0, rest - Panel.EdgeRows);
        }

        var upper = Math.Clamp(rest / 3, DescriptionShortest, DescriptionTallest);

        return Math.Max(0, rest - upper - Panel.EdgeRows);
    }

    /// <summary>
    /// How many Details fields show when the pane has <paramref name="rows"/> rows and <paramref name="count"/> fields:
    /// all of them when they fit, else as many as fit beside the pinned fold row (at least one).
    /// </summary>
    public static int Limit(int rows, int count)
    {
        var step = BlockRows + 1;

        if (rows <= 0)
        {
            return CreatePanes.ShownDetails;
        }

        if ((step * count) - 1 <= rows)
        {
            return count;
        }

        return Math.Max(1, rows / step);
    }

    /// <summary>The state with the Details rows of the active tab's create form brought up to the viewport.</summary>
    public static AppState Synced(AppState state)
    {
        var tab = state.Session.Active is { } id
            ? state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == id)
            : null;

        if (tab is not { Form: { Target: CreateFormTarget } form })
        {
            return state;
        }

        var rows = Rows(form, state.ViewportColumns, state.ViewportBodyHeight);

        if (rows == form.DetailsRows)
        {
            return state;
        }

        var fitted = form with { DetailsRows = rows };

        // A smaller viewport can fold the focused field: the focus moves to the last shown one, as with Ctrl-f.
        if (CreatePanes.IsFolded(fitted, fitted.Focus))
        {
            fitted = fitted with { Focus = CreatePanes.LastShown(fitted), Suggest = null };
        }

        return FormReducer.Replace(state, tab, fitted);
    }
}
