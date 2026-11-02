using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Edit;

/// <summary>
/// Edits the fields of one issue and assigns it. <see cref="GetEditFieldsAsync"/> and
/// <see cref="SearchAssignableUsersAsync"/> are reads; <see cref="UpdateAsync"/> and <see cref="AssignAsync"/> are
/// writes: call them only from a confirmed command, never retry them silently, and drop the cache of the issue and
/// of the views that may show it after a success and after an unknown outcome (the client keeps no cache). Works
/// the same for company-managed and team-managed projects.
/// </summary>
public sealed class EditClient(JiraTransport transport)
{
    private const string IssuePath = "/rest/api/3/issue";
    private const string EditMetaPart = "editmeta";
    private const string AssigneePart = "assignee";
    private const string AssignableSearchPath = "/rest/api/3/user/assignable/search";
    private const string AutomaticAccountId = "-1";
    private const int DefaultUserLimit = 50;
    private const int MaxUserLimit = 100;
    private const string NotFoundMessage = "Not found or no access.";

    /// <summary>
    /// The fields the account can edit on this issue now, each with its schema, operations and allowed values.
    /// Fetch it when the editor opens: it varies by issue type and status. Fields the app cannot write come back
    /// with <see cref="EditFieldKind.Unsupported"/> so the UI can show them read-only. A missing or hidden issue is
    /// <see cref="JiraErrorKind.NotFound"/>.
    /// </summary>
    public async Task<JiraResult<IReadOnlyList<EditField>>> GetEditFieldsAsync(
        string key,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return JiraResults.Failure<IReadOnlyList<EditField>>(
                new JiraError(JiraErrorKind.NotFound, null, NotFoundMessage));
        }

        var result = await transport.SendAsync(
            JiraRequest.Get($"{IssuePath}/{key}/{EditMetaPart}") with { Priority = priority },
            EditJsonContext.Default.Meta,
            cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(EditMapper.Map(result.Value))
            : JiraResults.Failure<IReadOnlyList<EditField>>(result.Error);
    }

    /// <summary>
    /// Sets, clears, adds or removes field values in one request. Jira applies all of it or none (a 400 names the
    /// fields, keyed by field id; a field not on the edit screen is such a 400). Jira has no undo and the reply
    /// carries no issue: reload it afterwards. The assignee has its own call, <see cref="AssignAsync"/>. A timeout
    /// or lost reply is an unknown outcome (reload with <see cref="ReadAfterWrite"/>).
    /// </summary>
    public Task<WriteResult<WriteAccepted>> UpdateAsync(
        string key,
        IReadOnlyList<FieldUpdate> updates,
        CancellationToken cancellationToken)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return Refused(WriteErrorKind.NotFound, NotFoundMessage);
        }

        if (updates.Count == 0)
        {
            return Refused(WriteErrorKind.Validation, "There is nothing to change.");
        }

        var rejection = Validate(updates);

        if (rejection is not null)
        {
            return Refused(WriteErrorKind.Validation, rejection);
        }

        var request = JiraRequest.Write(HttpMethod.Put, $"{IssuePath}/{key}", EditRequestBody.BuildUpdate(updates));

        return transport.SendWriteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Assigns the issue: to an account, to nobody (<see cref="AssigneeChange.Unassign"/>) or to the project's
    /// default (<see cref="AssigneeChange.Automatic"/>). A user who cannot be assigned is
    /// <see cref="WriteErrorKind.Validation"/>; a missing permission is <see cref="WriteErrorKind.Permission"/>.
    /// </summary>
    public Task<WriteResult<WriteAccepted>> AssignAsync(
        string key,
        AssigneeChange change,
        CancellationToken cancellationToken)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return Refused(WriteErrorKind.NotFound, NotFoundMessage);
        }

        if (change is AssigneeChange.AccountChange { AccountId: var accountId } && string.IsNullOrWhiteSpace(accountId))
        {
            return Refused(WriteErrorKind.Validation, "An account id is required.");
        }

        if (change is AssigneeChange.AccountChange { AccountId: AutomaticAccountId })
        {
            return Refused(WriteErrorKind.Validation, "Use the automatic assignee, not an account id.");
        }

        var request = JiraRequest.Write(
            HttpMethod.Put,
            $"{IssuePath}/{key}/{AssigneePart}",
            EditRequestBody.BuildAssignee(change));

        return transport.SendWriteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Accounts that can be assigned this issue, matching <paramref name="query"/> (name or email part). Minimal
    /// read for the assign picker; the shared suggestion sources are tracked in #213. The size is clamped to
    /// 1..100. An inactive account is never returned by Jira here.
    /// </summary>
    public async Task<JiraResult<IReadOnlyList<JiraUser>>> SearchAssignableUsersAsync(
        string key,
        string query,
        CancellationToken cancellationToken,
        int maxResults = DefaultUserLimit)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return JiraResults.Failure<IReadOnlyList<JiraUser>>(
                new JiraError(JiraErrorKind.NotFound, null, NotFoundMessage));
        }

        var limit = Math.Clamp(maxResults, 1, MaxUserLimit);
        var path = $"{AssignableSearchPath}?issueKey={key}&query={Uri.EscapeDataString(query)}&maxResults={limit}";

        var result = await transport.SendAsync(
            JiraRequest.Get(path),
            EditJsonContext.Default.ListJiraUser,
            cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success<IReadOnlyList<JiraUser>>(result.Value ?? [])
            : JiraResults.Failure<IReadOnlyList<JiraUser>>(result.Error);
    }

    private static string? Validate(IReadOnlyList<FieldUpdate> updates)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var update in updates)
        {
            if (string.IsNullOrWhiteSpace(update.FieldId))
            {
                return "A field id is required.";
            }

            if (EditMapper.IsAssignee(update.FieldId))
            {
                return "Use the assign call to change the assignee.";
            }

            if (!seen.Add(update.FieldId))
            {
                return $"Field {update.FieldId} is changed twice.";
            }
        }

        return null;
    }

    private static Task<WriteResult<WriteAccepted>> Refused(WriteErrorKind kind, string message) =>
        Task.FromResult(WriteResults.Failure<WriteAccepted>(new WriteError(kind, null, message, [], [])));
}
