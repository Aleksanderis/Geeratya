using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Issues;

/// <summary>
/// Reads one issue with the fields the detail view shows. Reads only; comments and remote links are separate
/// clients. A missing or hidden issue is a <see cref="JiraErrorKind.NotFound"/> ("not found or no access").
/// </summary>
public sealed class IssueClient(JiraTransport transport)
{
    private const string IssuePath = "/rest/api/3/issue";

    /// <summary>Loads an issue by key. A string that is not an issue key is answered as NotFound locally.</summary>
    public async Task<JiraResult<IssueDetail>> GetIssueAsync(
        string key,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return JiraResults.Failure<IssueDetail>(
                new JiraError(JiraErrorKind.NotFound, null, "Not found or no access."));
        }

        var request = JiraRequest.Get($"{IssuePath}/{key}?fields={IssueFields.DetailQueryValue}")
            with { Priority = priority };

        var result = await transport.SendAsync(request, IssueJsonContext.Default.Issue, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(IssueDetailMapper.Map(result.Value))
            : JiraResults.Failure<IssueDetail>(result.Error);
    }

    /// <summary>
    /// Reads the current values of <paramref name="fields"/> (the editable fields of the edit metadata), asking Jira
    /// for exactly those ids. A field the reply leaves out is absent from the result; one Jira holds no value for is
    /// present and empty.
    /// </summary>
    public async Task<JiraResult<IReadOnlyDictionary<string, IssueFieldValue>>> GetFieldValuesAsync(
        string key,
        IReadOnlyList<EditField> fields,
        CancellationToken cancellationToken)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return JiraResults.Failure<IReadOnlyDictionary<string, IssueFieldValue>>(
                new JiraError(JiraErrorKind.NotFound, null, "Not found or no access."));
        }

        var ids = string.Join(',', fields.Select(field => Uri.EscapeDataString(field.FieldId)));
        var request = JiraRequest.Get($"{IssuePath}/{key}?fields={ids}");

        var result = await transport.SendAsync(request, IssueJsonContext.Default.FieldBag, cancellationToken);

        if (!result.IsSuccess)
        {
            return JiraResults.Failure<IReadOnlyDictionary<string, IssueFieldValue>>(result.Error);
        }

        var values = new Dictionary<string, IssueFieldValue>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (result.Value.Values is { } bag && bag.TryGetValue(field.FieldId, out var raw))
            {
                if (IssueFieldValueMapper.Map(field.Kind, raw) is { } value)
                {
                    values[field.FieldId] = value;
                }
            }
        }

        return JiraResults.Success<IReadOnlyDictionary<string, IssueFieldValue>>(values);
    }
}
