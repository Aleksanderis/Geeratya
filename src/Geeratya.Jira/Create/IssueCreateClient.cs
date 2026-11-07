using System.Globalization;
using System.Text.RegularExpressions;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Create;

/// <summary>
/// Reads the create metadata of a project (issue types, then the fields of one type) and creates issues. The
/// metadata calls are reads; <see cref="CreateIssueAsync"/> is the one write and is only called from a confirmed
/// command. Metadata is fetched when the create form opens and never cached: it changes with the project's
/// configuration. After a create (or an unknown outcome) the caller invalidates the cache as the Writes section of
/// <c>docs/internal/jira-layer.md</c> says.
/// </summary>
public sealed partial class IssueCreateClient(JiraTransport transport)
{
    private const string CreateMetaPath = "/rest/api/3/issue/createmeta";
    private const string IssuePath = "/rest/api/3/issue";
    private const int PageSize = 50;
    private const int MaxPages = 20;

    /// <summary>The issue types the project offers on create, with hierarchy level and sub-task flag.</summary>
    public async Task<JiraResult<IReadOnlyList<CreateIssueType>>> GetIssueTypesAsync(
        string projectKey,
        CancellationToken cancellationToken)
    {
        if (!IsProjectKey(projectKey))
        {
            return JiraResults.Failure<IReadOnlyList<CreateIssueType>>(NotFound());
        }

        var result = new List<CreateIssueType>();
        var startAt = 0;

        for (var page = 0; page < MaxPages; page++)
        {
            var url = $"{CreateMetaPath}/{projectKey}/issuetypes?startAt={Number(startAt)}&maxResults={PageSize}";
            var reply = await transport.SendAsync(
                JiraRequest.Get(url),
                CreateJsonContext.Default.IssueTypePage,
                cancellationToken);

            if (!reply.IsSuccess)
            {
                return JiraResults.Failure<IReadOnlyList<CreateIssueType>>(reply.Error);
            }

            var items = reply.Value.IssueTypes ?? [];
            result.AddRange(CreateMetaMapper.MapIssueTypes(items));
            startAt += items.Count;

            if (IsLastPage(items.Count, startAt, reply.Value.Total))
            {
                break;
            }
        }

        return JiraResults.Success<IReadOnlyList<CreateIssueType>>(result);
    }

    /// <summary>The fields of the create screen for one issue type of the project.</summary>
    public async Task<JiraResult<IReadOnlyList<CreateField>>> GetFieldsAsync(
        string projectKey,
        string issueTypeId,
        CancellationToken cancellationToken)
    {
        if (!IsProjectKey(projectKey) || !IsIssueTypeId(issueTypeId))
        {
            return JiraResults.Failure<IReadOnlyList<CreateField>>(NotFound());
        }

        var result = new List<CreateField>();
        var startAt = 0;

        for (var page = 0; page < MaxPages; page++)
        {
            var url = $"{CreateMetaPath}/{projectKey}/issuetypes/{issueTypeId}"
                + $"?startAt={Number(startAt)}&maxResults={PageSize}";
            var reply = await transport.SendAsync(
                JiraRequest.Get(url),
                CreateJsonContext.Default.FieldPage,
                cancellationToken);

            if (!reply.IsSuccess)
            {
                return JiraResults.Failure<IReadOnlyList<CreateField>>(reply.Error);
            }

            var items = reply.Value.Fields ?? [];
            result.AddRange(CreateMetaMapper.MapFields(items));
            startAt += items.Count;

            if (IsLastPage(items.Count, startAt, reply.Value.Total))
            {
                break;
            }
        }

        return JiraResults.Success<IReadOnlyList<CreateField>>(result);
    }

    /// <summary>
    /// Creates the issue: <c>POST /issue</c>. Input that cannot be valid (blank summary, malformed keys, a field id
    /// the call sets itself) is refused as a validation error without a request. Success carries the new key; an
    /// unknown outcome must be settled by looking before repeating, a repeat would create a duplicate.
    /// </summary>
    public async Task<WriteResult<CreatedIssue>> CreateIssueAsync(NewIssue issue, CancellationToken cancellationToken)
    {
        var refused = Validate(issue);

        if (refused is not null)
        {
            return WriteResults.Failure<CreatedIssue>(refused);
        }

        var reply = await transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Post, IssuePath, CreateBodyWriter.Write(issue)),
            CreateJsonContext.Default.Created,
            cancellationToken);

        if (!reply.IsSuccess)
        {
            return WriteResults.Failure<CreatedIssue>(reply.Error);
        }

        // A reply without a key is unreadable: the issue exists, so the outcome is unknown, not a failure.
        if (reply.Value is not { Key: { Length: > 0 } key })
        {
            return WriteResults.Failure<CreatedIssue>(Unreadable());
        }

        return WriteResults.Success(new CreatedIssue(reply.Value.Id ?? string.Empty, key));
    }

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*\z")]
    private static partial Regex ProjectKeyPattern();

    [GeneratedRegex(@"^[0-9]+\z")]
    private static partial Regex IssueTypeIdPattern();

    private static bool IsProjectKey(string? key) =>
        key is { Length: > 0 and <= 255 } && ProjectKeyPattern().IsMatch(key);

    private static bool IsIssueTypeId(string? id) =>
        id is { Length: > 0 and <= 20 } && IssueTypeIdPattern().IsMatch(id);

    private static bool IsLastPage(int count, int nextStartAt, int? total) =>
        count == 0 || (total is { } known ? nextStartAt >= known : count < PageSize);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static JiraError NotFound() => new(JiraErrorKind.NotFound, null, "Not found or no access.");

    private static WriteError Unreadable() =>
        new(
            WriteErrorKind.UnknownOutcome,
            null,
            "Jira answered without the key of the new issue; it may have been created.",
            [],
            []);

    private static WriteError? Validate(NewIssue issue)
    {
        var errors = new List<WriteFieldError>();

        if (!IsProjectKey(issue.ProjectKey))
        {
            errors.Add(new WriteFieldError(CreateFieldIds.Project, "Not a project key."));
        }

        if (!IsIssueTypeId(issue.IssueTypeId))
        {
            errors.Add(new WriteFieldError(CreateFieldIds.IssueType, "Not an issue type id."));
        }

        if (string.IsNullOrWhiteSpace(issue.Summary))
        {
            errors.Add(new WriteFieldError(CreateFieldIds.Summary, "Summary is required."));
        }

        if (!string.IsNullOrWhiteSpace(issue.ParentKey) && !IssueUrls.IsValidKey(issue.ParentKey))
        {
            errors.Add(new WriteFieldError(CreateFieldIds.Parent, "Not an issue key."));
        }

        foreach (var fieldId in issue.Fields?.Keys ?? [])
        {
            if (IsReserved(fieldId))
            {
                errors.Add(new WriteFieldError(fieldId, "Set by the create call itself."));
            }
        }

        if (errors.Count == 0)
        {
            return null;
        }

        return new WriteError(WriteErrorKind.Validation, null, "The issue is not valid.", errors, []);
    }

    private static bool IsReserved(string fieldId) =>
        fieldId is CreateFieldIds.Project
            or CreateFieldIds.IssueType
            or CreateFieldIds.Summary
            or CreateFieldIds.Description
            or CreateFieldIds.Parent;
}
