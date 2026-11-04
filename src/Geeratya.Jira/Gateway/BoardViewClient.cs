using System.Globalization;
using System.Text.Json;
using Geeratya.Jira.Gateway.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Gateway;

/// <summary>
/// Reads a board's view settings from the Atlassian GraphQL gateway of the site (<c>/gateway/api/graphql</c>), where
/// Jira keeps "Hide done work items after": the agile API does not expose it. Read queries only, never a mutation
/// (Geeratya never writes this setting). The fields are experimental on Atlassian's side, so every shape problem is
/// an <see cref="JiraErrorKind.InvalidResponse"/> failure for the caller to fall back from.
/// Chain: the tenant's cloud id, then the board's default navigation item (its id holds the activation id), then the
/// board view node (the navigation item id with <c>navigation-item</c> replaced by <c>view</c>).
/// </summary>
public sealed class BoardViewClient(JiraTransport transport)
{
    public const string TenantPath = "/_edge/tenant_info";
    public const string GraphQlPath = "/gateway/api/graphql";

    private const string BoardAriPrefix = "ari:cloud:jira-software";
    private const string NavigationMarker = ":navigation-item/";
    private const string ViewMarker = ":view/";
    private const string JiraAriStart = "ari:cloud:jira:";

    private const string NavigationQuery =
        "query BoardNavigation($ids: [ID!]!) { jira_boardsByIds(ids: $ids) "
        + "{ defaultNavigationItem { ... on JiraNavigationItem { id } } } }";

    private const string CutoffQuery =
        "query BoardCutoff($id: ID!) { node(id: $id) { ... on JiraBoardView { completedIssueSearchCutOffInDays } } }";

    /// <summary>The cloud id of the site, the first link of the chain.</summary>
    public async Task<JiraResult<string>> GetCloudIdAsync(
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var request = JiraRequest.Get(TenantPath) with { Priority = priority };
        var result = await transport.SendAsync(request, GatewayJsonContext.Default.TenantInfo, cancellationToken);

        if (!result.IsSuccess)
        {
            return JiraResults.Failure<string>(result.Error);
        }

        return string.IsNullOrWhiteSpace(result.Value.CloudId)
            ? Invalid<string>("The site did not name its cloud id.")
            : JiraResults.Success(result.Value.CloudId);
    }

    /// <summary>
    /// The id of the board's view node: the default navigation item of the board with <c>navigation-item</c> read as
    /// <c>view</c>. A Scrum board's default item is its backlog, which carries no cutoff (see <see cref="GetCutoffAsync"/>).
    /// </summary>
    public async Task<JiraResult<string>> GetViewIdAsync(
        string cloudId,
        long boardId,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var ari = $"{BoardAriPrefix}:{cloudId}:board/{boardId.ToString(CultureInfo.InvariantCulture)}";
        var body = JsonSerializer.Serialize(
            new GatewayWire.IdsRequest(NavigationQuery, new GatewayWire.IdsVariables([ari])),
            GatewayJsonContext.Default.IdsRequest);
        var request = JiraRequest.PostSearch(GraphQlPath, body) with { Priority = priority };
        var result = await transport.SendAsync(request, GatewayJsonContext.Default.NavigationReply, cancellationToken);

        if (!result.IsSuccess)
        {
            return JiraResults.Failure<string>(result.Error);
        }

        if (result.Value.Errors is { Count: > 0 })
        {
            return Invalid<string>("The gateway refused the board lookup.");
        }

        var id = result.Value.Data?.Boards?.FirstOrDefault()?.DefaultNavigationItem?.Id;

        if (id is null || !id.StartsWith(JiraAriStart, StringComparison.Ordinal) || !id.Contains(NavigationMarker))
        {
            return Invalid<string>("The gateway named no navigation item for the board.");
        }

        return JiraResults.Success(id.Replace(NavigationMarker, ViewMarker, StringComparison.Ordinal));
    }

    /// <summary>
    /// The cutoff of a board view. A null value is "no cutoff" (a success); an absent field (the node is not a board
    /// view, as for a Scrum backlog), an error, or a value that is not a positive whole number is a failure.
    /// </summary>
    public async Task<JiraResult<BoardViewCutoff>> GetCutoffAsync(
        string viewId,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var body = JsonSerializer.Serialize(
            new GatewayWire.IdRequest(CutoffQuery, new GatewayWire.IdVariables(viewId)),
            GatewayJsonContext.Default.IdRequest);
        var request = JiraRequest.PostSearch(GraphQlPath, body) with { Priority = priority };
        var result = await transport.SendAsync(request, GatewayJsonContext.Default.CutoffReply, cancellationToken);

        if (!result.IsSuccess)
        {
            return JiraResults.Failure<BoardViewCutoff>(result.Error);
        }

        if (result.Value.Errors is { Count: > 0 })
        {
            return Invalid<BoardViewCutoff>("The gateway refused the view lookup.");
        }

        return MapCutoff(result.Value.Data?.Node);
    }

    private static JiraResult<BoardViewCutoff> MapCutoff(GatewayWire.BoardViewNode? node)
    {
        if (node is null)
        {
            return Invalid<BoardViewCutoff>("The gateway found no view for the board.");
        }

        var value = node.Cutoff;

        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                return JiraResults.Success(new BoardViewCutoff(null));
            case JsonValueKind.Number when value.TryGetInt32(out var days) && days > 0:
                return JiraResults.Success(new BoardViewCutoff(days));
            case JsonValueKind.Undefined:
                return Invalid<BoardViewCutoff>("The view has no done cutoff field.");
            default:
                return Invalid<BoardViewCutoff>("The done cutoff of the view is not a number of days.");
        }
    }

    private static JiraResult<T> Invalid<T>(string message)
    {
        return JiraResults.Failure<T>(new JiraError(JiraErrorKind.InvalidResponse, null, message));
    }
}
