using Geeratya.Core.Backlog;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Flash;

/// <summary>
/// The flash jump prompt (<c>nav.row-jump</c>, <c>&lt;space&gt;j</c>): type text, every visible row of the list that
/// contains it gets a letter, the letter moves the selection to that row, <c>Enter</c> takes the nearest one and
/// <c>Esc</c> cancels. It moves the selection only (read-only). Pure.
/// </summary>
public static class FlashJumpReducer
{
    private const string UnavailableMessage = "Jump to a visible row works in a list, board or backlog view, not inside an issue.";
    private const string NothingToJumpMessage = "No rows to jump to.";
    private const string NoRowsMessage = "No visible row contains that text.";
    private const string EmptyMessage = "Type some text of a visible row, or press Esc.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>Opens the prompt when the active tab shows rows or cards (list, columns or Scrum backlog).</summary>
    public static ReduceResult Open(AppState state)
    {
        if (Target(state) is null)
        {
            var inside = state.Session.Tabs.Any(tab => tab.Id == state.Session.Active && tab.Detail.Count > 0);

            return Plain(state with { Message = inside ? UnavailableMessage : NothingToJumpMessage });
        }

        var opened = state with
        {
            Mode = InputMode.Insert,
            FlashJump = new FlashJumpState(string.Empty),
            PendingKeys = string.Empty,
            Message = null,
        };

        return Plain(opened);
    }

    /// <summary>A typed key: a label letter jumps, anything else goes into the prompt.</summary>
    public static ReduceResult Type(AppState state, string text)
    {
        if (state.FlashJump is not { } flash)
        {
            return Plain(state);
        }

        var cleared = StatusMessages.Dismiss(state);

        if (flash.Text.Length > 0 && FlashLabeler.Select(LabelsOf(cleared, flash), text) is { } label)
        {
            return Jump(cleared, label);
        }

        return Plain(PromptEditing.Insert(cleared, text));
    }

    /// <summary>Enter: jumps to the nearest match.</summary>
    public static ReduceResult Accept(AppState state)
    {
        if (state.FlashJump is not { } flash)
        {
            return Plain(state);
        }

        var labels = LabelsOf(state, flash);

        if (labels.Count > 0)
        {
            return Jump(state, labels[0]);
        }

        return Plain(state with { Message = flash.Text.Length == 0 ? EmptyMessage : NoRowsMessage });
    }

    /// <summary>The labels the view shows for the open prompt: what the renderer draws and the keys select.</summary>
    public static IReadOnlyList<FlashLabel> LabelsOf(AppState state, FlashJumpState flash)
    {
        if (Target(state) is not { } tab)
        {
            return [];
        }

        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return FlashBoardLabeler.Compute(tab, flash.Text, state.ViewportColumns, state.ViewportBodyHeight);
        }

        var inner = ListViewColumns.InnerWidth(state.ViewportColumns, state.ViewportBodyHeight);

        return BacklogSections.Applies(tab)
            ? FlashLabeler.ComputeBacklog(tab, flash.Text, inner)
            : FlashLabeler.Compute(tab, flash.Text, inner, tab.VisibleRows);
    }

    private static ReduceResult Jump(AppState state, FlashLabel label)
    {
        var tab = Target(state)!;
        var closed = state with { Mode = InputMode.Normal, FlashJump = null, PendingKeys = string.Empty };

        if (tab.Definition.Layout == ViewLayout.Columns && label.Key is { } key)
        {
            return ViewReducer.SelectCard(closed, tab.Id, key, label.Lane);
        }

        return ViewReducer.Reduce(closed, new SelectionMoved(tab.Id, label.Row));
    }

    private static TabState? Target(AppState state)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        return tab is { Detail.Count: 0, Rows.Count: > 0 } ? tab : null;
    }

    private static ReduceResult Plain(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }
}
