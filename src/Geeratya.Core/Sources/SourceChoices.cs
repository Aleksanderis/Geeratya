using System.Globalization;
using Geeratya.Core.History;
using Geeratya.Core.Sprints;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Filters.Models;

namespace Geeratya.Core.Sources;

/// <summary>
/// The rows of the source picker: boards (a Board and a Backlog row each), saved filters and, last, the typed text as
/// a JQL query. Pure; the text filters boards and filters by case-insensitive substring.
/// </summary>
public static class SourceChoices
{
    private const int MaxRecentInAll = 10;

    public static IReadOnlyList<SourceChoice> For(SourcePickerState picker, SourceLists lists)
    {
        var text = picker.Text.Trim();
        var choices = new List<SourceChoice>();

        if (picker.Scope == PickerScope.Sprints)
        {
            return picker.Sprint is { } data ? SprintChoices.For(data, text) : choices;
        }

        if (picker.Scope is PickerScope.All or PickerScope.Boards or PickerScope.Backlogs)
        {
            AddBoards(choices, picker.Scope, text, lists.Boards ?? []);
        }

        if (picker.Scope is PickerScope.All or PickerScope.Filters)
        {
            AddFilters(choices, text, lists.Filters ?? []);
        }

        if (picker.Scope is PickerScope.All or PickerScope.Jql)
        {
            AddRecent(choices, picker.Scope, text, lists.History);
        }

        if (picker.Scope is PickerScope.All or PickerScope.Jql && text.Length > 0)
        {
            var jql = JqlText.FromTyped(text);

            choices.Add(new SourceChoice(SourceChoiceKind.Query, jql, string.Empty, JqlView(jql)));
        }

        return choices;
    }

    /// <summary>The view that opens a JQL query as a list.</summary>
    public static ViewDefinition JqlView(string jql)
    {
        return new ViewDefinition(ViewSourceKind.Jql, jql, ViewLayout.List, jql);
    }

    /// <summary>
    /// The view that opens a board (Columns) or its backlog (List); the backlog of a Scrum board is the sprint sections.
    /// A team-managed board reports <c>simple</c> and opens the plain backlog; the load swaps it to the Scrum backlog when the sprint probe says Scrum.
    /// </summary>
    public static ViewDefinition BoardView(BoardSummary board, bool backlog)
    {
        var id = board.Id.ToString(CultureInfo.InvariantCulture);

        if (backlog && board.Type == BoardType.Scrum)
        {
            return new ViewDefinition(ViewSourceKind.ScrumBacklog, id, ViewLayout.List, board.Name);
        }

        return backlog
            ? new ViewDefinition(ViewSourceKind.Backlog, id, ViewLayout.List, board.Name)
            : new ViewDefinition(ViewSourceKind.Board, id, ViewLayout.Columns, board.Name);
    }

    /// <summary>The view that opens a saved filter as a list.</summary>
    public static ViewDefinition FilterView(SavedFilter filter)
    {
        return new ViewDefinition(ViewSourceKind.SavedFilter, filter.Id, ViewLayout.List, filter.Name);
    }

    /// <summary>Favourites first, then the caller's own filters, each filter once (by id).</summary>
    public static IReadOnlyList<SavedFilter> MergeFilters(
        IReadOnlyList<SavedFilter> favourites,
        IReadOnlyList<SavedFilter> own)
    {
        var merged = new List<SavedFilter>();
        var seen = new HashSet<string>();
        var starred = favourites.Select(filter => filter.Id).ToHashSet();

        foreach (var filter in favourites.Concat(own))
        {
            if (seen.Add(filter.Id))
            {
                merged.Add(starred.Contains(filter.Id) ? filter with { IsFavourite = true } : filter);
            }
        }

        return merged.OrderBy(filter => filter.IsFavourite ? 0 : 1).ToList();
    }

    private static void AddBoards(List<SourceChoice> choices, PickerScope scope, string text, IReadOnlyList<BoardSummary> boards)
    {
        foreach (var board in boards.Where(board => Contains(board.Name, text)))
        {
            var detail = BoardDetail(board);

            if (scope is PickerScope.All or PickerScope.Boards)
            {
                choices.Add(new SourceChoice(SourceChoiceKind.Board, board.Name, detail, BoardView(board, false)));
            }

            if (scope is PickerScope.All or PickerScope.Backlogs)
            {
                choices.Add(new SourceChoice(SourceChoiceKind.Backlog, board.Name, detail, BoardView(board, true)));
            }
        }
    }

    private static void AddFilters(List<SourceChoice> choices, string text, IReadOnlyList<SavedFilter> filters)
    {
        foreach (var filter in filters.Where(filter => Contains(filter.Name, text)))
        {
            choices.Add(new SourceChoice(SourceChoiceKind.Filter, filter.Name, FilterDetail(filter), FilterView(filter)));
        }
    }

    private static void AddRecent(List<SourceChoice> choices, PickerScope scope, string text, SiteHistory? history)
    {
        var typed = text.Length == 0 ? string.Empty : JqlText.FromTyped(text);
        var limit = scope == PickerScope.All ? MaxRecentInAll : SiteHistory.MaxQueries;
        var recent = (history?.Queries ?? [])
            .Where(query => Contains(query, text) && !string.Equals(query, typed, StringComparison.Ordinal))
            .Take(limit);

        foreach (var query in recent)
        {
            choices.Add(new SourceChoice(SourceChoiceKind.Recent, query, string.Empty, JqlView(query)));
        }
    }

    private static bool Contains(string name, string text)
    {
        return text.Length == 0 || name.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static string BoardDetail(BoardSummary board)
    {
        var type = board.Type switch
        {
            BoardType.Kanban => "kanban",
            BoardType.Scrum => "scrum",
            BoardType.Simple => "simple",
            _ => string.Empty,
        };

        var parts = new[] { type, board.ProjectKey }.Where(part => part.Length > 0).ToArray();

        return parts.Length == 0 ? string.Empty : $"({string.Join(", ", parts)})";
    }

    private static string FilterDetail(SavedFilter filter)
    {
        if (filter.IsFavourite)
        {
            return "(favourite)";
        }

        return string.IsNullOrEmpty(filter.OwnerName) ? string.Empty : $"({filter.OwnerName})";
    }
}
