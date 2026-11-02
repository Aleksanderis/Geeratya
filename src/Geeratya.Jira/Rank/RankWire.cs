using System.Text.Json;

namespace Geeratya.Jira.Rank;

/// <summary>The JSON shape of the 207 multi-status reply of the rank and backlog calls. Every member is optional.</summary>
internal static class RankWire
{
    internal sealed record Response(List<Entry?>? Entries);

    internal sealed record Entry(string? IssueKey, int? Status, JsonElement? Errors);
}
