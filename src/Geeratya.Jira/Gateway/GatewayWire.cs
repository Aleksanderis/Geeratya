using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geeratya.Jira.Gateway;

/// <summary>
/// The JSON shapes of the tenant lookup and of the Atlassian GraphQL gateway as they are sent and read. Every member
/// of a reply is optional: the gateway is experimental, so a missing or renamed part is a failure the caller falls
/// back from, never an exception.
/// </summary>
internal static class GatewayWire
{
    internal sealed record TenantInfo(string? CloudId);

    internal sealed record IdsRequest(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("variables")] IdsVariables Variables);

    internal sealed record IdsVariables([property: JsonPropertyName("ids")] string[] Ids);

    internal sealed record IdRequest(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("variables")] IdVariables Variables);

    internal sealed record IdVariables([property: JsonPropertyName("id")] string Id);

    internal sealed record NavigationReply(
        [property: JsonPropertyName("data")] NavigationData? Data,
        [property: JsonPropertyName("errors")] List<GraphQlError>? Errors);

    internal sealed record NavigationData(
        [property: JsonPropertyName("jira_boardsByIds")] List<BoardEntry?>? Boards);

    internal sealed record BoardEntry(
        [property: JsonPropertyName("defaultNavigationItem")] NavigationItem? DefaultNavigationItem);

    internal sealed record NavigationItem([property: JsonPropertyName("id")] string? Id);

    internal sealed record CutoffReply(
        [property: JsonPropertyName("data")] CutoffData? Data,
        [property: JsonPropertyName("errors")] List<GraphQlError>? Errors);

    internal sealed record CutoffData([property: JsonPropertyName("node")] BoardViewNode? Node);

    /// <summary>
    /// The board view node. The cutoff is a <see cref="JsonElement"/> so an absent field (the node is not a board
    /// view) can be told from an explicit null (Jira applies no cutoff).
    /// </summary>
    internal sealed record BoardViewNode(
        [property: JsonPropertyName("completedIssueSearchCutOffInDays")] JsonElement Cutoff);

    internal sealed record GraphQlError([property: JsonPropertyName("message")] string? Message);
}
