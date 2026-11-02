using Geeratya.Jira.Issues;
using Geeratya.Jira.Links.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Links;

/// <summary>
/// Link types (a read) and creating and removing issue links (writes: call them only from a confirmed command,
/// never retry them silently; the client keeps no cache). Works the same for company-managed and team-managed
/// projects. A link created with the API answers 201 with an empty body, so its id is found by reading the issue.
/// </summary>
public sealed class LinkClient(JiraTransport transport)
{
    /// <summary>The field id a refused issue key is reported under, so a form can show it at the key field.</summary>
    public const string IssueFieldId = "issue";

    private const string TypesPath = "/rest/api/3/issueLinkType";
    private const string LinkPath = "/rest/api/3/issueLink";
    private const string NotFoundMessage = "Not found or no access.";

    /// <summary>The link types of the site, in Jira's order.</summary>
    public async Task<JiraResult<IReadOnlyList<IssueLinkType>>> GetLinkTypesAsync(
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var result = await transport.SendAsync(
            JiraRequest.Get(TypesPath) with { Priority = priority },
            LinkJsonContext.Default.Types,
            cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(LinkMapper.Map(result.Value))
            : JiraResults.Failure<IReadOnlyList<IssueLinkType>>(result.Error);
    }

    /// <summary>
    /// Links two issues (<c>POST /issueLink</c>, 201). The parameters are the values of Jira's own fields:
    /// <paramref name="inwardKey"/> is the issue that reads the type's outward wording ("blocks"),
    /// <paramref name="outwardKey"/> the one that reads the inward wording ("is blocked by"). A malformed key,
    /// or both keys the same, is refused here (<see cref="WriteErrorKind.Validation"/> at
    /// <see cref="IssueFieldId"/>); a missing issue or permission is Jira's 404 or 403.
    /// </summary>
    public Task<WriteResult<WriteAccepted>> CreateLinkAsync(
        string typeId,
        string outwardKey,
        string inwardKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            return Refused(WriteErrorKind.Validation, "A link type is required.", null);
        }

        if (!IssueUrls.IsValidKey(outwardKey) || !IssueUrls.IsValidKey(inwardKey))
        {
            return Refused(WriteErrorKind.Validation, "Use an issue key such as PROJ-123.", IssueFieldId);
        }

        if (string.Equals(outwardKey, inwardKey, StringComparison.OrdinalIgnoreCase))
        {
            return Refused(WriteErrorKind.Validation, "An issue cannot be linked to itself.", IssueFieldId);
        }

        var request = JiraRequest.Write(
            HttpMethod.Post,
            LinkPath,
            LinkRequestBody.Build(typeId, outwardKey, inwardKey));

        return transport.SendWriteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Removes a link (<c>DELETE /issueLink/{id}</c>, 204). Jira has no undo. A lost reply is an unknown outcome
    /// (read the issue: a link that is gone means done).
    /// </summary>
    public Task<WriteResult<WriteAccepted>> DeleteLinkAsync(string linkId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(linkId) || !linkId.All(char.IsAsciiDigit))
        {
            return Refused(WriteErrorKind.NotFound, NotFoundMessage, null);
        }

        var request = JiraRequest.Write(HttpMethod.Delete, $"{LinkPath}/{linkId}", null);

        return transport.SendWriteAsync(request, cancellationToken);
    }

    private static Task<WriteResult<WriteAccepted>> Refused(WriteErrorKind kind, string message, string? fieldId)
    {
        IReadOnlyList<WriteFieldError> fields = fieldId is null ? [] : [new WriteFieldError(fieldId, message)];

        return Task.FromResult(WriteResults.Failure<WriteAccepted>(new WriteError(kind, null, message, fields, [])));
    }
}
