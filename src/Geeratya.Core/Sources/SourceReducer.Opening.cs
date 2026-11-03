using Geeratya.Core.History;
using Geeratya.Core.Keymap;
using Geeratya.Core.SavedViews;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Sources;

public static partial class SourceReducer
{
    /// <summary>Opens the saved-view picker (<c>view.saved-open</c>).</summary>
    public static ReduceResult OpenSavedViews(AppState state)
    {
        return SavedViewReducer.Palette(state, string.Empty);
    }

    private static bool IsFresh(HistoryLoaded loaded, AppState state)
    {
        return loaded.Profile == state.Profiles.Active && loaded.Version > state.Session.Sources.HistoryVersion;
    }

    /// <summary>Opens the picker on the given scope with the text typed (used by <see cref="SavedViewReducer"/>).</summary>
    public static ReduceResult OpenScoped(AppState state, PickerScope scope, string text)
    {
        return Open(state, scope, text, autoOpen: false);
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
        var needsViews = scope is PickerScope.All or PickerScope.Views && lists.SavedViews is null;

        if (needsHistory && state.Profiles.Active is { } profile)
        {
            effects.Add(new LoadHistoryEffect(profile));
        }

        if (needsViews && state.Profiles.Active is { } owner)
        {
            effects.Add(new LoadSavedViewsEffect(owner));
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
