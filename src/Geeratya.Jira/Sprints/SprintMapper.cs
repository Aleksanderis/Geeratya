using System.Globalization;
using Geeratya.Jira.Boards;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Jira.Sprints;

/// <summary>Maps the wire shapes of the sprint endpoints. Pure and tolerant: a missing field is neutral.</summary>
internal static class SprintMapper
{
    public static OffsetPage<Sprint> MapList(SprintWire.SprintList wire)
    {
        var values = wire.Values ?? [];
        var items = new List<Sprint>(values.Count);

        foreach (var sprint in values)
        {
            items.Add(MapSprint(sprint));
        }

        var startAt = wire.StartAt ?? 0;
        var isLast = BoardMapper.IsLast(startAt, items.Count, wire.Total, wire.IsLast);

        return new OffsetPage<Sprint>(items, startAt, wire.MaxResults ?? items.Count, wire.Total, isLast);
    }

    public static Sprint MapSprint(SprintWire.Sprint wire) =>
        new(
            wire.Id ?? 0,
            wire.Name ?? string.Empty,
            ParseState(wire.State),
            string.IsNullOrWhiteSpace(wire.Goal) ? null : wire.Goal,
            ParseDate(wire.StartDate),
            ParseDate(wire.EndDate),
            ParseDate(wire.CompleteDate),
            wire.OriginBoardId ?? 0);

    public static SprintState ParseState(string? state) =>
        state?.ToLowerInvariant() switch
        {
            "future" => SprintState.Future,
            "active" => SprintState.Active,
            "closed" => SprintState.Closed,
            _ => SprintState.Unknown,
        };

    public static string StateName(SprintState state) =>
        state switch
        {
            SprintState.Future => "future",
            SprintState.Active => "active",
            SprintState.Closed => "closed",
            _ => string.Empty,
        };

    private static DateTimeOffset? ParseDate(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;
}
