using System.Globalization;
using Geeratya.Core.History;
using Geeratya.Core.Keymap;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Sources;

/// <summary>
/// The source part of the reducer: the <c>:jql</c>, <c>:filter</c>, <c>:board</c> and <c>:backlog</c> commands and
/// the picker overlay. Pure; the host loads the boards and filters (<see cref="LoadSourcesEffect"/>) and answers with
/// <see cref="SourceCommand"/>s. Every opened view is a new tab.
/// </summary>
public static class SourceReducer
{
    private const string NoProfileMessage = "No profile yet: add one with :profile add.";
    private const string NothingToOpenMessage = "Nothing to open: type JQL, or press Esc.";
    private const string NoOpenSprintMessage = "No active or future sprint on this board.";
    private const string NoBoardMessage = "No board here: open a Scrum board first (:board).";
    private const string LoadingMessage = "Still loading: wait a moment, or press Esc.";
    private const char DoubleQuote = '"';
    private const char SingleQuote = '\'';

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Reduce(AppState state, SourceCommand command)
    {
        if (command is SprintListLoaded or SprintsFailed)
        {
            return Plain(SprintsArrived(state, command));
        }

        var lists = state.Session.Sources;

        var next = command switch
        {
            BoardsLoaded loaded => lists with { Boards = loaded.Boards, BoardsLoading = false, BoardsError = null },
            FiltersLoaded loaded => lists with { Filters = loaded.Filters, FiltersLoading = false, FiltersError = null },
            SourceListFailed failed => lists.Failed(failed.Kind, LoadErrorText.For(failed.Error)),
            HistoryLoaded loaded when IsFresh(loaded, state) => lists with { History = loaded.History, HistoryVersion = loaded.Version },
            _ => lists,
        };

        return AutoOpen(WithLists(state, next));
    }

    /// <summary>Runs <c>jql</c>, <c>filter</c>, <c>board</c> or <c>backlog</c> with the text after the word.</summary>
    public static ReduceResult Palette(AppState state, string verb, string argument)
    {
        if (state.Profiles.Active is null)
        {
            return Plain(state with { Message = NoProfileMessage });
        }

        if (verb == PaletteCommands.Jql && argument.Trim().Length == 0)
        {
            return Open(state, PickerScope.Jql, string.Empty, autoOpen: false);
        }

        if (verb == PaletteCommands.Jql)
        {
            return OpenJql(state, argument.Trim());
        }

        var name = Unquote(argument);
        var scope = ScopeOf(verb);

        return Open(state, scope, name, autoOpen: scope != PickerScope.Filters || name.Length > 0);
    }

    /// <summary><c>:history clear</c>: forgets the active profile's recent queries and issues (local data only).</summary>
    public static ReduceResult ClearHistory(AppState state)
    {
        if (state.Profiles.Active is not { } profile)
        {
            return Plain(state with { Message = NoProfileMessage });
        }

        var lists = state.Session.Sources with { History = SiteHistory.Empty };
        var cleared = WithLists(state, lists) with { Message = "History cleared." };

        return new ReduceResult(cleared, [new ClearHistoryEffect(profile)]);
    }

    /// <summary>
    /// Remembers an opened JQL string or issue key of the active profile: the loaded copy changes at once and the
    /// host writes the file.
    /// </summary>
    public static ReduceResult Recorded(ReduceResult result, HistoryKind kind, string text)
    {
        var state = result.State;

        if (state.Profiles.Active is not { } profile || text.Trim().Length == 0)
        {
            return result;
        }

        var lists = state.Session.Sources;

        if (lists.History is { } history)
        {
            var updated = kind == HistoryKind.Query ? history.WithQuery(text) : history.WithIssue(text);

            state = WithLists(state, lists with { History = updated });
        }

        return new ReduceResult(state, [.. result.Effects, new RecordHistoryEffect(profile, kind, text)]);
    }

    /// <summary>Opens the empty source picker (<c>&lt;space&gt;t</c>, <c>:tab new</c>).</summary>
    public static ReduceResult OpenPicker(AppState state)
    {
        if (state.Profiles.Active is null)
        {
            return Plain(state with { Message = NoProfileMessage });
        }

        return Open(state, PickerScope.All, string.Empty, autoOpen: false);
    }

    public static AppState Type(AppState state, string text)
    {
        var picker = state.Picker;

        if (picker is null)
        {
            return state;
        }

        return state with { Picker = picker with { Text = $"{picker.Text}{text}", Selected = 0, AutoOpen = false, Notice = null } };
    }

    public static AppState Backspace(AppState state)
    {
        var picker = state.Picker;

        if (picker is null || picker.Text.Length == 0)
        {
            return state;
        }

        var starts = StringInfo.ParseCombiningCharacters(picker.Text);
        var text = picker.Text[..starts[^1]];

        return state with { Picker = picker with { Text = text, Selected = 0, AutoOpen = false, Notice = null } };
    }

    /// <summary>Moves the highlight by <paramref name="delta"/> rows, stopping at the first and last row.</summary>
    public static AppState Move(AppState state, int delta)
    {
        var picker = state.Picker;

        if (picker is null)
        {
            return state;
        }

        var count = SourceChoices.For(picker, state.Session.Sources).Count;
        var current = Math.Clamp(picker.Selected, 0, Math.Max(0, count - 1));
        var selected = Math.Clamp(current + delta, 0, Math.Max(0, count - 1));

        return state with { Picker = picker with { Selected = selected, AutoOpen = false, Notice = null } };
    }

    /// <summary>
    /// Opens the sprint switcher for the board of the active tab and asks the host for the board's sprints. The list is
    /// loaded on every open, because sprints are started and completed in between.
    /// </summary>
    public static ReduceResult OpenSprintPicker(AppState state)
    {
        return OpenSprintPicker(state, null);
    }

    /// <summary>
    /// Opens the same list to choose the sprint the issues <paramref name="moveKeys"/> move into (active and future
    /// sprints only); null opens the switcher.
    /// </summary>
    public static ReduceResult OpenSprintPicker(AppState state, IReadOnlyList<string>? moveKeys)
    {
        if (state.Profiles.Active is null)
        {
            return Plain(state with { Message = NoProfileMessage });
        }

        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        if (tab is null || SprintBoards.BoardOf(tab.Definition) is not { } board)
        {
            return Plain(state with { Message = NoBoardMessage });
        }

        var picker = new SourcePickerState(string.Empty, 0, PickerScope.Sprints, false, null)
        {
            Sprint = new SprintPickerData(board, tab.Id, null, null, SprintChoices.ClosedPage) { MoveKeys = moveKeys },
        };

        var opened = Close(state) with { Mode = InputMode.Insert, Picker = picker, Message = null };

        return new ReduceResult(opened, [new LoadSprintsEffect(board)]);
    }

    /// <summary>Like <see cref="Accept"/>, but a sprint always opens in a new tab.</summary>
    public static ReduceResult AcceptInNewTab(AppState state, DateTimeOffset at)
    {
        return state.Picker is { Scope: PickerScope.Sprints } ? AcceptSprint(state, true, at) : Accept(state, at);
    }

    /// <summary>
    /// Opens the highlighted row in a new tab and closes the picker. In the sprint switcher the row replaces the sprint
    /// of the tab it was opened from when that is a Sprint view, else it opens in a new tab.
    /// </summary>
    public static ReduceResult Accept(AppState state, DateTimeOffset at)
    {
        var picker = state.Picker;

        if (picker is null)
        {
            return Plain(state);
        }

        if (picker.Scope == PickerScope.Sprints)
        {
            return AcceptSprint(state, false, at);
        }

        var choices = SourceChoices.For(picker, state.Session.Sources);

        if (choices.Count == 0)
        {
            return Plain(state with { Picker = picker with { Notice = EmptyNotice(picker, state.Session.Sources) } });
        }

        var choice = choices[Math.Clamp(picker.Selected, 0, choices.Count - 1)];

        return OpenView(state, choice.Definition);
    }

    /// <summary>Closes the picker and leaves Insert mode.</summary>
    public static AppState Close(AppState state)
    {
        return state with
        {
            Mode = InputMode.Normal,
            Picker = null,
            PendingKeys = string.Empty,
            PaletteText = string.Empty,
        };
    }

    private static ReduceResult AcceptSprint(AppState state, bool newTab, DateTimeOffset at)
    {
        var picker = state.Picker!;
        var choices = SourceChoices.For(picker, state.Session.Sources);

        if (choices.Count == 0)
        {
            return Plain(state with { Picker = picker with { Notice = SprintEmptyNotice(picker) } });
        }

        var choice = choices[Math.Clamp(picker.Selected, 0, choices.Count - 1)];
        var data = picker.Sprint!;

        if (choice.Kind == SourceChoiceKind.MoreClosed)
        {
            var more = data with { ClosedShown = data.ClosedShown + SprintChoices.ClosedPage };

            return Plain(state with { Picker = picker with { Sprint = more, Notice = null } });
        }

        var origin = state.Session.Tabs.FirstOrDefault(tab => tab.Id == data.Origin);

        if (data.MoveKeys is not null)
        {
            // The picker chose the target of a move: nothing opens, the confirmed write starts.
            return SprintMoveActions.Choose(Close(state), data, choice, at);
        }

        if (!newTab && origin is { Definition.Source: ViewSourceKind.Sprint }
            && SprintSource.TryParse(choice.Definition.SourceValue, out var source))
        {
            return ViewReducer.Reduce(Close(state), new SprintSwitched(origin.Id, source, choice.Name));
        }

        return OpenView(state, choice.Definition);
    }

    private static string SprintEmptyNotice(SourcePickerState picker)
    {
        var data = picker.Sprint!;

        if (data.Error is { } error)
        {
            return error;
        }

        if (data.Sprints is null)
        {
            return LoadingMessage;
        }

        return data.MoveKeys is null ? NothingToOpenMessage : NoOpenSprintMessage;
    }

    /// <summary>The sprints (or the failure) of the board the open switcher asked for; a reply for another board is dropped.</summary>
    private static AppState SprintsArrived(AppState state, SourceCommand command)
    {
        if (state.Picker is not { Scope: PickerScope.Sprints, Sprint: { } data } picker)
        {
            return state;
        }

        var updated = command switch
        {
            SprintListLoaded loaded when loaded.BoardId == data.BoardId => data with { Sprints = loaded.Sprints, Error = null },
            SprintsFailed failed when failed.BoardId == data.BoardId && data.Sprints is null
                => data with { Error = LoadErrorText.For(failed.Error) },
            _ => data,
        };

        return state with { Picker = picker with { Sprint = updated } };
    }

    private static bool IsFresh(HistoryLoaded loaded, AppState state)
    {
        return loaded.Profile == state.Profiles.Active && loaded.Version > state.Session.Sources.HistoryVersion;
    }

    private static ReduceResult Open(AppState state, PickerScope scope, string text, bool autoOpen)
    {
        var picker = new SourcePickerState(text, 0, scope, autoOpen, null);
        var opened = Close(state) with { Mode = InputMode.Insert, Picker = picker, Message = null };
        var effects = new List<Effect>();
        var lists = opened.Session.Sources;
        var boards = NeedsBoards(scope) && RequestIfNeeded(ref lists, SourceListKind.Boards);
        var filters = NeedsFilters(scope) && RequestIfNeeded(ref lists, SourceListKind.Filters);

        var needsHistory = scope is PickerScope.All or PickerScope.Jql && lists.History is null;

        if (needsHistory && state.Profiles.Active is { } profile)
        {
            effects.Add(new LoadHistoryEffect(profile));
        }

        if (boards || filters)
        {
            effects.Add(new LoadSourcesEffect(boards, filters));
        }

        var result = AutoOpen(WithLists(opened, lists));

        return effects.Count == 0 ? result : new ReduceResult(result.State, [.. effects, .. result.Effects]);
    }

    private static bool RequestIfNeeded(ref SourceLists lists, SourceListKind kind)
    {
        if (lists.IsLoaded(kind) || lists.IsLoading(kind))
        {
            return false;
        }

        lists = lists.Requested(kind);

        return true;
    }

    private static bool NeedsBoards(PickerScope scope)
    {
        return scope is PickerScope.All or PickerScope.Boards or PickerScope.Backlogs;
    }

    private static bool NeedsFilters(PickerScope scope)
    {
        return scope is PickerScope.All or PickerScope.Filters;
    }

    /// <summary>
    /// A palette command waits for its list: once it is loaded (or failed) a unique match opens the view, anything
    /// else leaves the picker open with a notice.
    /// </summary>
    private static ReduceResult AutoOpen(AppState state)
    {
        var picker = state.Picker;

        if (picker is null || !picker.AutoOpen)
        {
            return Plain(state);
        }

        var kind = picker.Scope == PickerScope.Filters ? SourceListKind.Filters : SourceListKind.Boards;
        var lists = state.Session.Sources;

        if (lists.ErrorOf(kind) is not null)
        {
            return Plain(state with { Picker = picker with { AutoOpen = false } });
        }

        if (!lists.IsLoaded(kind))
        {
            return Plain(state);
        }

        return Resolve(state, picker, lists, kind);
    }

    private static ReduceResult Resolve(AppState state, SourcePickerState picker, SourceLists lists, SourceListKind kind)
    {
        var names = NamesOf(lists, kind);
        var text = picker.Text.Trim();
        var match = text.Length == 0 ? OnlyOne(names.Count) : NameMatcher.Find(names, text);

        if (match.Kind == NameMatchKind.Unique)
        {
            return OpenView(state, ViewOf(picker.Scope, lists, match.Index));
        }

        var notice = text.Length == 0 ? null : MatchNotice(picker.Scope, text, match);

        return Plain(state with { Picker = picker with { AutoOpen = false, Notice = notice } });
    }

    private static IReadOnlyList<string> NamesOf(SourceLists lists, SourceListKind kind)
    {
        if (kind == SourceListKind.Boards)
        {
            return [.. lists.Boards!.Select(board => board.Name)];
        }

        return [.. lists.Filters!.Select(filter => filter.Name)];
    }

    /// <summary>With no name typed, a list of exactly one entry is the match.</summary>
    private static NameMatch OnlyOne(int count)
    {
        return count == 1 ? new NameMatch(NameMatchKind.Unique, 0, 1) : new NameMatch(NameMatchKind.None, -1, count);
    }

    private static ViewDefinition ViewOf(PickerScope scope, SourceLists lists, int index)
    {
        return scope switch
        {
            PickerScope.Filters => SourceChoices.FilterView(lists.Filters![index]),
            _ => SourceChoices.BoardView(lists.Boards![index], scope == PickerScope.Backlogs),
        };
    }

    private static string MatchNotice(PickerScope scope, string text, NameMatch match)
    {
        var noun = scope == PickerScope.Filters ? "filter" : "board";

        return match.Kind == NameMatchKind.Ambiguous
            ? $"\"{text}\" matches {match.Count} {noun}s: choose one."
            : $"No {noun} named \"{text}\".";
    }

    private static string EmptyNotice(SourcePickerState picker, SourceLists lists)
    {
        var kind = picker.Scope == PickerScope.Filters ? SourceListKind.Filters : SourceListKind.Boards;

        if (lists.IsLoading(kind) && !lists.IsLoaded(kind))
        {
            return LoadingMessage;
        }

        return NothingToOpenMessage;
    }

    private static ReduceResult OpenJql(AppState state, string query)
    {
        var opened = ViewReducer.Reduce(state, new ViewOpenRequested(SourceChoices.JqlView(query)));

        return Recorded(opened, HistoryKind.Query, query);
    }

    private static ReduceResult OpenView(AppState state, ViewDefinition definition)
    {
        var opened = ViewReducer.Reduce(Close(state), new ViewOpenRequested(definition));

        if (definition.Source != ViewSourceKind.Jql)
        {
            return opened;
        }

        return Recorded(opened, HistoryKind.Query, definition.SourceValue);
    }

    private static PickerScope ScopeOf(string verb)
    {
        return verb switch
        {
            PaletteCommands.Board => PickerScope.Boards,
            PaletteCommands.Backlog => PickerScope.Backlogs,
            _ => PickerScope.Filters,
        };
    }

    /// <summary>A name in matching quotes is used without them (<c>:board "My board"</c>).</summary>
    private static string Unquote(string argument)
    {
        var text = argument.Trim();

        if (text.Length >= 2 && text[0] is DoubleQuote or SingleQuote && text[^1] == text[0])
        {
            return text[1..^1].Trim();
        }

        return text;
    }

    private static AppState WithLists(AppState state, SourceLists lists)
    {
        return state with { Session = state.Session with { Sources = lists } };
    }

    private static ReduceResult Plain(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }
}
