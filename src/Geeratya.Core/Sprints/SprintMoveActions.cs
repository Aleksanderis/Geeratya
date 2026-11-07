using Geeratya.Core.Backlog;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
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
/// The sprint moves (<c>docs/internal/ux-keymap.md</c>): the issue under the cursor, the marked issues or the Visual range (or
/// the open issue) go to a sprint chosen in the picker, or to the backlog. The set is the same one every write uses
/// (<see cref="RowSelection.Targets"/>), so a move never widens or narrows silently. Pure: the question for more than
/// one issue and the rest of the write belong to <see cref="WriteReducer"/>.
/// </summary>
public static class SprintMoveActions
{
    private const string NeedsBoardText = "Sprint moves work in a board, backlog or sprint view";
    private const string KanbanText = "This is a Kanban board: it has no sprints.";
    private const string NothingText = "Nothing to move here";
    private const string CollapsedText = "Expand the section first (z a): its issues are hidden";
    private const string SprintsNotLoadedText = "The sprints are still loading: drop again in a moment.";
    private const string SprintUnavailableText = "That sprint is not available: refresh the view.";
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

    /// <summary>
    /// A row dragged from one section of a Scrum backlog onto another (header, goal, note or row line): asks to move
    /// the marks or Visual range when the pressed row belongs to them, else that row alone, to that sprint or the
    /// backlog. Null when this is not such a drop (another layout, a drop inside one section, a press that is not on a
    /// row), so the drag selects rows instead. Never writes: the request always asks first.
    /// </summary>
    public static ReduceResult? Dropped(AppState state, RowsDragged drag, DateTimeOffset at)
    {
        var tab = ActiveTab(state);

        if (tab is null || state.Mode != InputMode.Normal || tab.Detail.Count > 0 || !BacklogSections.Applies(tab)
            || tab.Board is { Type: BoardType.Kanban })
        {
            return null;
        }

        var layout = BacklogSections.Build(tab);
        var to = layout.LineAt(LineOf(tab, drag.ToRow));
        var home = PressedRow(tab, layout, drag);

        if (home is not { } row || to is not { } target || target.Section.Key == layout.SectionOfRow(row)?.Key
            || !OnScreen(tab, drag.ToRow))
        {
            return null;
        }

        if (target.Section.Sprint is null && !target.Section.IsBacklog)
        {
            return Say(state, tab.Sprints.Count == 0 ? SprintsNotLoadedText : SprintUnavailableText);
        }

        var keys = DraggedKeys(tab, row);
        var sprint = target.Section.Sprint is { } found ? new IssueSprint(found.Id, found.Name, found.State) : null;

        return Start(state, keys, sprint, at, dropped: true);
    }

    // The row the button went down on. After a live drag the window may have scrolled since, so the press cell no
    // longer points at it: the Visual anchor (set by the first move) is that row.
    private static int? PressedRow(TabState tab, BacklogLayout layout, RowsDragged drag)
    {
        if (drag.Live && tab.VisualAnchor is { } anchor)
        {
            return BacklogCursor.IndexOfKey(tab, anchor);
        }

        return OnScreen(tab, drag.FromRow) ? layout.LineAt(LineOf(tab, drag.FromRow))?.Row : null;
    }

    private static bool OnScreen(TabState tab, int screenRow)
    {
        return screenRow >= ListLayout.HeaderRows && screenRow - ListLayout.HeaderRows < tab.VisibleRows;
    }

    private static int LineOf(TabState tab, int screenRow)
    {
        return tab.Scroll + screenRow - ListLayout.HeaderRows;
    }

    // The set the pressed row belongs to: the marks or the Visual range when it is in them, else the row alone.
    private static IReadOnlyList<string> DraggedKeys(TabState tab, int row)
    {
        var key = tab.Rows[row].Key;

        if (tab.Marks.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            return RowSelection.Targets(tab);
        }

        if (RowSelection.VisualRange(tab) is { } range && row >= range.First && row <= range.Last)
        {
            return RowSelection.Targets(tab with { Marks = [] });
        }

        return [key];
    }

    private static ReduceResult Start(
        AppState state,
        IReadOnlyList<string> keys,
        IssueSprint? target,
        DateTimeOffset at,
        bool dropped = false)
    {
        var tab = ActiveTab(state);

        if (tab is null)
        {
            return Say(state, NothingText);
        }

        var sprintsKnown = tab.Rows.Any(row => row.CurrentSprint is not null);
        var movable = keys.Where(key => !IsIn(state, key, target, sprintsKnown)).ToList();

        if (movable.Count == 0)
        {
            return Say(state, AlreadyInText(keys.Count, target));
        }

        var request = new SprintMoveRequest(movable, target, SourceOf(state, movable), dropped);

        return WriteReducer.Reduce(state, new WriteRequested(tab.Id, request, at));
    }

    // Issues already in the target are left out, so the Confirm dialog counts exactly what will be sent. A row that is
    // not loaded stays in: Jira decides.
    // Without any sprint on the loaded rows (the Sprint field was not read) "no sprint" says nothing about the
    // backlog, so the backlog is never filtered then.
    private static bool IsIn(AppState state, string key, IssueSprint? target, bool sprintsKnown)
    {
        if (FindRow(state, key) is not { } row || (target is null && !sprintsKnown))
        {
            return false;
        }

        var sprint = row.CurrentSprint is { State: SprintState.Active or SprintState.Future } open ? open : null;

        return target is null ? sprint is null : sprint is not null && sprint.Id == target.Id;
    }

    private static string AlreadyInText(int count, IssueSprint? target)
    {
        var subject = count == 1 ? "That issue is" : "Those issues are";

        return $"{subject} already in {TargetName(target)}";
    }

    private static string TargetName(IssueSprint? target)
    {
        return target is null ? BacklogText : Quoted(target.Name);
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

        if (tab.Marks.Count == 0 && BacklogCursor.OnCollapsedHeader(tab))
        {
            refusal = CollapsedText;

            return null;
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
    public static string SourceOf(AppState state, IReadOnlyList<string> keys)
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
        var active = ActiveTab(state);
        var tabs = active is null ? state.Session.Tabs : [active, .. state.Session.Tabs];

        return tabs
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
