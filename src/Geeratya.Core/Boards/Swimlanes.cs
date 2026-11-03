using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Boards;

/// <summary>
/// Swimlanes of the columns layout (#192). The agile API does not expose the lanes a board defines in Jira, so the
/// lanes are Geeratya's own grouping of the loaded issues by assignee or epic; the layout shows one lane at a time
/// (all its columns), and the lane can be changed with <c>]]</c> and <c>[[</c>. Pure.
/// </summary>
public static class Swimlanes
{
    /// <summary>The name of the lane of issues without an assignee.</summary>
    public const string UnassignedName = "Unassigned";

    /// <summary>The name of the lane of issues under no epic.</summary>
    public const string NoEpicName = "No epic";

    /// <summary>The lane key of the leftover lane (Unassigned or No epic).</summary>
    public const string LeftoverKey = "";

    /// <summary>The next grouping in the cycle none, assignee, epic.</summary>
    public static SwimlaneKind Next(SwimlaneKind kind)
    {
        return kind switch
        {
            SwimlaneKind.None => SwimlaneKind.Assignee,
            SwimlaneKind.Assignee => SwimlaneKind.Epic,
            _ => SwimlaneKind.None,
        };
    }

    /// <summary>The words for a grouping in the status line and header.</summary>
    public static string Label(SwimlaneKind kind)
    {
        return kind switch
        {
            SwimlaneKind.Assignee => "assignee",
            SwimlaneKind.Epic => "epic",
            _ => "none",
        };
    }

    /// <summary>
    /// The lanes of <paramref name="rows"/> in order of first appearance, the leftover lane last. Empty for no
    /// grouping or no rows.
    /// </summary>
    public static IReadOnlyList<SwimlaneLane> Lanes(SwimlaneKind kind, IReadOnlyList<IssueSummary> rows)
    {
        if (kind == SwimlaneKind.None || rows.Count == 0)
        {
            return [];
        }

        var order = new List<string>();
        var names = new Dictionary<string, string>();
        var counts = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            var key = KeyOf(kind, row);

            if (counts.TryGetValue(key, out var count))
            {
                counts[key] = count + 1;

                continue;
            }

            counts[key] = 1;
            names[key] = NameOf(kind, row);
            order.Add(key);
        }

        if (order.Remove(LeftoverKey))
        {
            order.Add(LeftoverKey);
        }

        return [.. order.Select(key => new SwimlaneLane(key, names[key], counts[key]))];
    }

    /// <summary>The lane <paramref name="laneKey"/> names, or the first lane when it is null or has vanished.</summary>
    public static SwimlaneLane? Current(IReadOnlyList<SwimlaneLane> lanes, string? laneKey)
    {
        if (lanes.Count == 0)
        {
            return null;
        }

        return lanes.FirstOrDefault(lane => lane.Key == laneKey) ?? lanes[0];
    }

    /// <summary>The lane <paramref name="direction"/> places away from the current one, wrapping around.</summary>
    public static SwimlaneLane? Step(IReadOnlyList<SwimlaneLane> lanes, string? laneKey, int direction)
    {
        if (Current(lanes, laneKey) is not { } current)
        {
            return null;
        }

        var index = lanes.ToList().IndexOf(current);

        return lanes[((index + direction) % lanes.Count + lanes.Count) % lanes.Count];
    }

    /// <summary>The rows of the current lane; every row when there is no grouping.</summary>
    public static IReadOnlyList<IssueSummary> Filter(
        SwimlaneKind kind,
        string? laneKey,
        IReadOnlyList<IssueSummary> rows)
    {
        if (kind == SwimlaneKind.None)
        {
            return rows;
        }

        var current = Current(Lanes(kind, rows), laneKey);

        return current is null ? rows : [.. rows.Where(row => KeyOf(kind, row) == current.Key)];
    }

    private static string KeyOf(SwimlaneKind kind, IssueSummary row)
    {
        return kind == SwimlaneKind.Assignee ? row.Assignee?.AccountId ?? LeftoverKey : row.Epic?.Key ?? LeftoverKey;
    }

    private static string NameOf(SwimlaneKind kind, IssueSummary row)
    {
        if (kind == SwimlaneKind.Assignee)
        {
            return row.Assignee?.DisplayName ?? UnassignedName;
        }

        if (row.Epic is not { } epic)
        {
            return NoEpicName;
        }

        return string.IsNullOrWhiteSpace(epic.Summary) ? epic.Key : $"{epic.Key} {epic.Summary}";
    }
}
