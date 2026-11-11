using System.Globalization;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Backlog;

/// <summary>
/// Groups the rows of a Scrum backlog tab into sections: the active sprints, then the future ones (each by id, which is
/// the order Jira creates them in), then the backlog. Pure. The tab keeps its rows in this order (<see cref="Order"/>),
/// so a section is a run of rows and every other feature that reads <c>Rows[Selected]</c> works unchanged; rank order
/// is kept inside a section.
/// </summary>
public static class BacklogSections
{
    public const string BacklogKey = "backlog";
    public const string BacklogName = "Backlog";

    private const string SprintKeyPrefix = "sprint:";
    private const int ActiveRank = 0;
    private const int FutureRank = 1;
    private const int BacklogRank = 2;

    /// <summary>Whether the tab shows its rows as sections.</summary>
    public static bool Applies(TabState tab)
    {
        return tab.Definition.Source == ViewSourceKind.ScrumBacklog;
    }

    /// <summary>
    /// The layout of the tab. Sprints from the sprint list that have no row yet show as empty sections; a sprint seen
    /// only on a row shows with the name and state of that row. Memoized on the tab (<see cref="ColumnsMemo"/>).
    /// </summary>
    public static BacklogLayout Build(TabState tab)
    {
        return tab.Columns.GetBacklog(tab, Compute);
    }

    private static BacklogLayout Compute(TabState tab)
    {
        var sections = new List<SectionDraft>();
        var rows = tab.Rows;
        var index = 0;

        while (index < rows.Count)
        {
            var id = IdOf(rows[index]);
            var start = index;
            var points = 0.0;
            var estimated = false;

            while (index < rows.Count && IdOf(rows[index]) == id)
            {
                if (rows[index].Estimate is { } estimate)
                {
                    points += estimate;
                    estimated = true;
                }

                index++;
            }

            var state = rows[start].CurrentSprint?.State;

            var draft = new SectionDraft(KeyOfId(id), NameOf(rows[start]), state, start, index - start, points, estimated);

            sections.Add(draft);
        }

        AddEmptySprints(sections, tab);

        return Finish(sections, tab);
    }

    /// <summary>
    /// The rows in section order. The same instance when they already are, so a load that changes nothing keeps its
    /// references; the sort is stable, which keeps the rank order inside a section.
    /// </summary>
    public static IReadOnlyList<IssueSummary> Order(IReadOnlyList<IssueSummary> rows)
    {
        var previous = new SectionOrder(int.MinValue, long.MinValue);
        var ordered = true;

        foreach (var row in rows)
        {
            var current = OrderOf(row);

            if (current.CompareTo(previous) < 0)
            {
                ordered = false;

                break;
            }

            previous = current;
        }

        return ordered ? rows : rows.OrderBy(row => OrderOf(row).Rank).ThenBy(row => OrderOf(row).Id).ToArray();
    }

    /// <summary>The key of the section a row belongs to.</summary>
    public static string KeyOf(IssueSummary row)
    {
        return row.CurrentSprint is { State: SprintState.Active or SprintState.Future } sprint
            ? SprintKey(sprint.Id)
            : BacklogKey;
    }

    /// <summary>The sprint id of the section a row belongs to; 0 for the backlog. Allocates nothing.</summary>
    private static long IdOf(IssueSummary row)
    {
        return row.CurrentSprint is { State: SprintState.Active or SprintState.Future } sprint ? sprint.Id : 0;
    }

    private static string KeyOfId(long id)
    {
        return id == 0 ? BacklogKey : SprintKey(id);
    }

    public static string SprintKey(long sprintId)
    {
        return $"{SprintKeyPrefix}{sprintId.ToString(CultureInfo.InvariantCulture)}";
    }

    private static SectionOrder OrderOf(IssueSummary row)
    {
        return row.CurrentSprint is { State: SprintState.Active or SprintState.Future } sprint
            ? new SectionOrder(RankOf(sprint.State), sprint.Id)
            : new SectionOrder(BacklogRank, 0);
    }

    private static int RankOf(SprintState state)
    {
        return state == SprintState.Active ? ActiveRank : FutureRank;
    }

    private static string NameOf(IssueSummary row)
    {
        return row.CurrentSprint is { State: SprintState.Active or SprintState.Future } sprint
            ? sprint.Name
            : BacklogName;
    }

    private static void AddEmptySprints(List<SectionDraft> sections, TabState tab)
    {
        foreach (var sprint in tab.Sprints)
        {
            if (sprint.State is not (SprintState.Active or SprintState.Future))
            {
                continue;
            }

            var key = SprintKey(sprint.Id);

            if (sections.Any(section => section.Key == key))
            {
                continue;
            }

            var order = new SectionOrder(RankOf(sprint.State), sprint.Id);
            var at = sections.FindIndex(section => OrderOfSection(section).CompareTo(order) > 0);
            var position = at < 0 ? sections.Count : at;
            var row = position < sections.Count ? sections[position].FirstRow : tab.Rows.Count;

            sections.Insert(position, new SectionDraft(key, sprint.Name, sprint.State, row, 0, 0, false));
        }
    }

    private static SectionOrder OrderOfSection(SectionDraft section)
    {
        if (section.Key == BacklogKey)
        {
            return new SectionOrder(BacklogRank, 0);
        }

        var id = long.Parse(section.Key.AsSpan(SprintKeyPrefix.Length), CultureInfo.InvariantCulture);

        return new SectionOrder(RankOf(section.State ?? SprintState.Future), id);
    }

    private static BacklogLayout Finish(List<SectionDraft> drafts, TabState tab)
    {
        var sections = new List<BacklogSection>(drafts.Count);
        var line = 0;

        foreach (var draft in drafts)
        {
            var sprint = SprintOf(tab, draft.Key);
            var section = new BacklogSection(
                draft.Key,
                sprint?.Name ?? draft.Name,
                sprint,
                draft.RowCount > 0 ? draft.State : sprint?.State ?? draft.State,
                draft.FirstRow,
                draft.RowCount,
                draft.Points,
                draft.HasEstimates,
                tab.CollapsedSections.Contains(draft.Key),
                line);

            sections.Add(section);
            line += section.LineCount;
        }

        return new BacklogLayout(sections);
    }

    private static Sprint? SprintOf(TabState tab, string key)
    {
        foreach (var sprint in tab.Sprints)
        {
            if (SprintKey(sprint.Id) == key)
            {
                return sprint;
            }
        }

        return null;
    }

    private sealed record SectionDraft(
        string Key,
        string Name,
        SprintState? State,
        int FirstRow,
        int RowCount,
        double Points,
        bool HasEstimates);
}
