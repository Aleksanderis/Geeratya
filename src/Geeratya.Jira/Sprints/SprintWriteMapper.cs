using System.Globalization;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Jira.Sprints;

/// <summary>Maps the sprint object of a write reply. A reply without an id is not a sprint.</summary>
internal static class SprintWriteMapper
{
    public static WrittenSprint? Map(SprintWriteWire.Sprint? wire)
    {
        if (wire?.Id is not { } id)
        {
            return null;
        }

        return new WrittenSprint(
            id,
            wire.Name ?? string.Empty,
            ParseState(wire.State),
            wire.OriginBoardId,
            string.IsNullOrEmpty(wire.Goal) ? null : wire.Goal,
            ParseDate(wire.StartDate),
            ParseDate(wire.EndDate),
            ParseDate(wire.CompleteDate));
    }

    private static WrittenSprintState ParseState(string? state) =>
        state?.ToLowerInvariant() switch
        {
            SprintWriteBody.FutureState => WrittenSprintState.Future,
            SprintWriteBody.ActiveState => WrittenSprintState.Active,
            SprintWriteBody.ClosedState => WrittenSprintState.Closed,
            _ => WrittenSprintState.Unknown,
        };

    private static DateTimeOffset? ParseDate(string? text)
    {
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            return date;
        }

        return null;
    }
}
