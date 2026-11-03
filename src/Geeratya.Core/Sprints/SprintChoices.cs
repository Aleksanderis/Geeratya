using System.Globalization;
using Geeratya.Core.Boards;
using Geeratya.Core.Sources;
using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// The rows of the sprint switcher: the active sprints, the future ones in Jira's order, then the closed ones newest
/// first, as many as <see cref="SprintPickerData.ClosedShown"/> allows and a "more" row for the rest. Pure; the text
/// filters by case-insensitive substring of the name.
/// </summary>
public static class SprintChoices
{
    /// <summary>The closed sprints shown first and added by each "more" row.</summary>
    public const int ClosedPage = 10;

    public static IReadOnlyList<SourceChoice> For(SprintPickerData data, string text)
    {
        if (data.Sprints is not { } sprints)
        {
            return [];
        }

        var matching = sprints
            .Where(sprint => text.Length == 0 || sprint.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var choices = new List<SourceChoice>();

        choices.AddRange(InState(matching, SprintState.Active).Select(sprint => Choice(data, sprint)));
        choices.AddRange(InState(matching, SprintState.Future).Select(sprint => Choice(data, sprint)));

        var closed = InState(matching, SprintState.Closed)
            .OrderByDescending(sprint => sprint.CompleteDate ?? DateTimeOffset.MinValue)
            .ThenByDescending(sprint => sprint.Id)
            .ToList();

        choices.AddRange(closed.Take(data.ClosedShown).Select(sprint => Choice(data, sprint)));

        if (closed.Count > data.ClosedShown)
        {
            choices.Add(More(data, closed.Count - data.ClosedShown));
        }

        return choices;
    }

    /// <summary>The Sprint view of one sprint.</summary>
    public static ViewDefinition ViewOf(long boardId, Sprint sprint)
    {
        var source = new SprintSource(boardId, sprint.Id).ToString();

        return new ViewDefinition(ViewSourceKind.Sprint, source, ViewLayout.Columns, sprint.Name);
    }

    private static IEnumerable<Sprint> InState(List<Sprint> sprints, SprintState state)
    {
        return sprints.Where(sprint => sprint.State == state);
    }

    private static SourceChoice Choice(SprintPickerData data, Sprint sprint)
    {
        return new SourceChoice(SourceChoiceKind.Sprint, sprint.Name, Detail(sprint), ViewOf(data.BoardId, sprint))
        {
            Sprint = sprint,
        };
    }

    private static SourceChoice More(SprintPickerData data, int left)
    {
        var count = left.ToString(CultureInfo.InvariantCulture);
        var name = left == 1 ? "1 more closed sprint" : $"{count} more closed sprints";
        var placeholder = new ViewDefinition(
            ViewSourceKind.Sprint,
            SprintSource.Active(data.BoardId).ToString(),
            ViewLayout.Columns,
            name);

        return new SourceChoice(SourceChoiceKind.MoreClosed, name, string.Empty, placeholder);
    }

    private static string Detail(Sprint sprint)
    {
        var parts = new List<string>();
        var dates = SprintHeaderText.Dates(sprint);

        if (dates.Length > 0)
        {
            parts.Add(dates);
        }

        if (sprint.State == SprintState.Closed && SprintHeaderText.DaysLeft(sprint, default) is { Length: > 0 } closed)
        {
            parts.Add(closed);
        }

        return parts.Count == 0 ? string.Empty : $"({string.Join(", ", parts)})";
    }
}
