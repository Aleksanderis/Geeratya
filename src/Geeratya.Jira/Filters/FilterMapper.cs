using System.Globalization;
using Geeratya.Jira.Filters.Models;
using Geeratya.Jira.Paging;

namespace Geeratya.Jira.Filters;

/// <summary>Maps the wire shapes of the filter endpoints. Pure and tolerant: a missing field is neutral.</summary>
internal static class FilterMapper
{
    public static OffsetPage<SavedFilter> MapPage(FilterWire.FilterPage wire)
    {
        var values = wire.Values ?? [];
        var items = MapList(values);
        var startAt = wire.StartAt ?? 0;
        var isLast = values.Count == 0 || wire.IsLast == true
            || (wire.Total is { } total ? startAt + values.Count >= total : wire.IsLast ?? false);

        return new OffsetPage<SavedFilter>(items, startAt, wire.MaxResults ?? values.Count, wire.Total, isLast);
    }

    public static IReadOnlyList<SavedFilter> MapList(IReadOnlyList<FilterWire.Filter> wire)
    {
        var items = new List<SavedFilter>(wire.Count);

        foreach (var filter in wire)
        {
            items.Add(Map(filter));
        }

        return items;
    }

    public static SavedFilter Map(FilterWire.Filter wire) =>
        new(
            wire.Id ?? string.Empty,
            wire.Name ?? string.Empty,
            NullIfBlank(wire.Jql),
            NullIfBlank(wire.Owner?.DisplayName),
            wire.Favourite ?? false,
            ParseTime(wire.ApproximateLastUsed));

    /// <summary>Jira writes <c>2026-11-01T13:51:54.500+0000</c> (offset without a colon).</summary>
    private static DateTimeOffset? ParseTime(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
