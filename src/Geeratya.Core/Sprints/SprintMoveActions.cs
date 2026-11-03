using Geeratya.Core.Selection;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// The sprint moves (<c>docs/ux-keymap.md</c>): the issue under the cursor, the marked issues or the Visual range (or
/// the open issue) go to a sprint chosen in the picker, or to the backlog. The set is the same one every write uses
/// (<see cref="RowSelection.Targets"/>), so a move never widens or narrows silently. Pure: the question for more than
/// one issue and the rest of the write belong to <see cref="WriteReducer"/>.
/// </summary>
public static class SprintMoveActions
{
    private const string NeedsBoardText = "Sprint moves work in a board, backlog or sprint view";
    private const string KanbanText = "This is a Kanban board: it has no sprints.";
    private const string NothingText = "Nothing to move here";
    private const string BacklogText = "the backlog";
    private const int MaxPlacesNamed = 2;

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>Opens the picker of active and future sprints for the set; says why not when there is none.</summary>
    public static ReduceResult OpenPicker(AppState state)
    {
        var keys = KeysOf(state, out var refusal);

        return keys is null
            ? Say(state, refusal!)
            : SourceReducer.OpenSprintPicker(state, keys);
    }

    /// <summary>Moves the set to the backlog (asks first when it holds more than one issue).</summary>
    public static ReduceResult ToBacklog(AppState state, DateTimeOffset at)
    {
        var keys = KeysOf(state, out var refusal);

        return keys is null
            ? Say(state, refusal!)
            : Start(state, keys, null, at);
    }

    /// <summary>The picker chose a row: moves the keys it was opened with into that sprint.</summary>
    public static ReduceResult Choose(AppState state, SprintPickerData data, SourceChoice choice, DateTimeOffset at)
    {
        if (choice.Sprint is not { } sprint)
        {
            return Say(state, NothingText);
        }

        var target = new IssueSprint(sprint.Id, sprint.Name, sprint.State);

        return Start(state, data.MoveKeys!, target, at);
    }

    private static ReduceResult Start(
        AppState state,
        IReadOnlyList<string> keys,
        IssueSprint? target,
        DateTimeOffset at)
    {
        var tab = ActiveTab(state);

        if (tab is null)
        {
            return Say(state, NothingText);
        }

        var request = new SprintMoveRequest(keys, target, SourceOf(state, keys));

        return WriteReducer.Reduce(state, new WriteRequested(tab.Id, request, at));
    }

    private static IReadOnlyList<string>? KeysOf(AppState state, out string? refusal)
    {
        refusal = null;

        var tab = ActiveTab(state);

        if (tab is null)
        {
            refusal = NothingText;

            return null;
        }

        if (SprintBoards.BoardOf(tab.Definition) is null)
        {
            refusal = NeedsBoardText;

            return null;
        }

        if (tab.Board is { Type: BoardType.Kanban })
        {
            refusal = KanbanText;

            return null;
        }

        if (tab.Detail.Count > 0)
        {
            return [tab.Detail[^1].Key];
        }

        var keys = RowSelection.Targets(tab);

        if (keys.Count == 0)
        {
            refusal = NothingText;

            return null;
        }

        return keys;
    }

    /// <summary>Where the issues are now: their open sprints (up to two named), else the backlog.</summary>
    private static string SourceOf(AppState state, IReadOnlyList<string> keys)
    {
        var names = new List<string>();
        var inBacklog = false;

        foreach (var key in keys)
        {
            var sprint = FindRow(state, key)?.CurrentSprint;

            if (sprint is { State: SprintState.Active or SprintState.Future })
            {
                if (!names.Contains(sprint.Name, StringComparer.Ordinal))
                {
                    names.Add(sprint.Name);
                }
            }
            else
            {
                inBacklog = true;
            }
        }

        var places = names.Select(Quoted).ToList();

        if (inBacklog)
        {
            places.Add(BacklogText);
        }

        return places.Count switch
        {
            0 => BacklogText,
            <= MaxPlacesNamed => string.Join(" and ", places),
            _ => $"{places.Count} places",
        };
    }

    private static string Quoted(string name)
    {
        return $"sprint \"{name}\"";
    }

    private static IssueSummary? FindRow(AppState state, string key)
    {
        return state.Session.Tabs
            .SelectMany(tab => tab.Rows)
            .FirstOrDefault(row => string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Tabs.FirstOrDefault(tab => tab.Id == state.Session.Active);
    }

    private static ReduceResult Say(AppState state, string text)
    {
        return new ReduceResult(state with { Message = text }, _noEffects);
    }
}
