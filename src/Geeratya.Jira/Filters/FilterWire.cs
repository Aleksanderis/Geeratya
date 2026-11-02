namespace Geeratya.Jira.Filters;

/// <summary>The JSON shapes of the filter endpoints as Jira sends them. Every member is optional.</summary>
internal static class FilterWire
{
    internal sealed record FilterPage(List<Filter>? Values, int? StartAt, int? MaxResults, int? Total, bool? IsLast);

    internal sealed record Filter(
        string? Id,
        string? Name,
        string? Jql,
        Owner? Owner,
        bool? Favourite,
        string? ApproximateLastUsed);

    internal sealed record Owner(string? DisplayName);
}
