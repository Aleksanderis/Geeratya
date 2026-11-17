using Geeratya.Core.Boards;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Forms;

/// <summary>
/// Opens the status forms from a key: the picker for the issue the tab shows or has selected, and the move of the
/// selected card to the neighbouring board column. Pure; the transitions themselves are read by the form's metadata
/// load, so nothing here knows a workflow.
/// </summary>
public static class TransitionForms
{
    private const string NoIssueText = "No issue selected: select a row or open an issue first";
    private const string NeedsColumnsText = "Moving a card needs the columns layout of a board";
    private const string NoCardText = "No card selected";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>Opens the picker (<c>issue.transition</c>).</summary>
    public static ReduceResult OpenPicker(AppState state)
    {
        var tab = ActiveTab(state);
        var key = tab is null ? null : FormReducer.TargetKey(state, tab);

        if (tab is null || key is null)
        {
            return Say(state, NoIssueText);
        }

        var target = new TransitionPickTarget(key, [], null, []);

        return FormReducer.Reduce(state, new OpenFormRequested(tab.Id, target));
    }

    /// <summary>
    /// Moves the selected card <paramref name="step"/> columns (1 next, -1 previous): the transition that ends in the
    /// column is sent at once when there is exactly one, else the picker opens (<c>issue.transition-next</c>).
    /// </summary>
    public static ReduceResult MoveCard(AppState state, int step)
    {
        var tab = ActiveTab(state);

        if (tab is not { Board: { } board } || tab.Definition.Layout != ViewLayout.Columns || tab.Detail.Count > 0)
        {
            return Say(state, NeedsColumnsText);
        }

        if (ColumnsNavigation.SelectedIssue(tab) is not { } issue)
        {
            return Say(state, NoCardText);
        }

        var from = IndexOfColumn(board.Columns.Select(column => column.StatusIds), issue.Status.Id);

        if (from < 0)
        {
            return Say(state, $"{issue.Key} is in no board column; use the status picker");
        }

        var to = from + step;

        // The hidden Backlog column is not a target: the layout does not draw it.
        var firstShown = BoardColumns.HidesBacklogColumn(board) ? 1 : 0;

        if (to < firstShown || to >= board.Columns.Count)
        {
            return Say(state, $"{issue.Key} is already in the {(step > 0 ? "last" : "first")} column");
        }

        var column = board.Columns[to];

        if (column.StatusIds.Count == 0)
        {
            return Say(state, $"The column {column.Name} holds no status");
        }

        var target = new TransitionPickTarget(issue.Key, column.StatusIds, column.Name, []);

        return FormReducer.Reduce(state, new OpenFormRequested(tab.Id, target));
    }

    private static int IndexOfColumn(IEnumerable<IReadOnlyList<string>> columns, string statusId)
    {
        var index = 0;

        foreach (var statuses in columns)
        {
            if (statuses.Contains(statusId))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }
}
