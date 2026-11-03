using Geeratya.Jira.Search;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Fields;

/// <summary>
/// Reads the site's field list (<c>GET /rest/api/3/field</c>) to find the Sprint and estimate fields. Reads only.
/// The Sprint field is the one whose schema is <c>com.pyxis.greenhopper.jira:gh-sprint</c>; estimate fields are the
/// ones Jira Software names "Story point estimate" and "Story Points" (a board's own estimation field, from its
/// configuration, is preferred where the board is known).
/// </summary>
public sealed class FieldClient(JiraTransport transport)
{
    public const string FieldPath = "/rest/api/3/field";
    public const string SprintSchema = "com.pyxis.greenhopper.jira:gh-sprint";

    private static readonly string[] _estimateNames = ["Story point estimate", "Story Points"];

    public async Task<JiraResult<IssueExtraFields>> GetExtraFieldsAsync(
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var request = JiraRequest.Get(FieldPath) with { Priority = priority };
        var result = await transport.SendAsync(request, FieldJsonContext.Default.ListField, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(Map(result.Value))
            : JiraResults.Failure<IssueExtraFields>(result.Error);
    }

    internal static IssueExtraFields Map(List<FieldWire.Field>? fields)
    {
        string? sprint = null;
        var estimates = new List<(int Rank, string Id)>();

        foreach (var field in fields ?? [])
        {
            if (field?.Id is not { Length: > 0 } id)
            {
                continue;
            }

            if (sprint is null && field.Schema?.Custom == SprintSchema)
            {
                sprint = id;
            }

            var rank = Array.FindIndex(
                _estimateNames,
                name => string.Equals(name, field.Name, StringComparison.OrdinalIgnoreCase));

            if (rank >= 0)
            {
                estimates.Add((rank, id));
            }
        }

        return new IssueExtraFields(sprint, [.. estimates.OrderBy(entry => entry.Rank).Select(entry => entry.Id)]);
    }
}
