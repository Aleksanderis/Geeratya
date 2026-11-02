using System.Text.Json;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Transitions;

/// <summary>
/// Lists the workflow transitions of one issue and performs one. Listing is a read; <see cref="PerformAsync"/> is a
/// write: call it only from a confirmed command, never retry it silently, and drop the cache of the issue and of
/// the views that may show it after a success and after an unknown outcome (the client keeps no cache). Works the
/// same for company-managed and team-managed projects (team-managed transitions are global).
/// </summary>
public sealed class TransitionClient(JiraTransport transport)
{
    private const string IssuePath = "/rest/api/3/issue";
    private const string TransitionsPath = "transitions";
    private const string ListQuery = "?expand=transitions.fields";

    /// <summary>
    /// The transitions the account can do now, with the fields each screen asks for. A missing or hidden issue is
    /// <see cref="JiraErrorKind.NotFound"/> ("not found or no access").
    /// </summary>
    public async Task<JiraResult<IReadOnlyList<Transition>>> GetTransitionsAsync(
        string key,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return JiraResults.Failure<IReadOnlyList<Transition>>(
                new JiraError(JiraErrorKind.NotFound, null, "Not found or no access."));
        }

        var result = await transport.SendAsync(
            JiraRequest.Get($"{IssuePath}/{key}/{TransitionsPath}{ListQuery}") with { Priority = priority },
            TransitionJsonContext.Default.Page,
            cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(TransitionMapper.Map(result.Value))
            : JiraResults.Failure<IReadOnlyList<Transition>>(result.Error);
    }

    /// <summary>
    /// Moves the issue through <paramref name="transitionId"/>, sending <paramref name="fields"/> (field id to its
    /// JSON value, as the field's schema wants it) and an optional plain-text <paramref name="comment"/>. Jira has
    /// no undo and the reply carries no issue: reload it afterwards. A missing required field is
    /// <see cref="WriteErrorKind.Validation"/> keyed by field id; a timeout or lost reply is an unknown outcome
    /// (reload with <see cref="ReadAfterWrite"/>).
    /// </summary>
    public Task<WriteResult<WriteAccepted>> PerformAsync(
        string key,
        string transitionId,
        IReadOnlyDictionary<string, JsonElement>? fields,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return Task.FromResult(Refused(WriteErrorKind.NotFound, "Not found or no access."));
        }

        if (string.IsNullOrWhiteSpace(transitionId))
        {
            return Task.FromResult(Refused(WriteErrorKind.Validation, "A transition id is required."));
        }

        if (fields?.Values.Any(value => value.ValueKind == JsonValueKind.Undefined) == true)
        {
            return Task.FromResult(Refused(WriteErrorKind.Validation, "A field value is missing."));
        }

        var request = JiraRequest.Write(
            HttpMethod.Post,
            $"{IssuePath}/{key}/{TransitionsPath}",
            TransitionRequestBody.Build(transitionId, fields, comment));

        return transport.SendWriteAsync(request, cancellationToken);
    }

    private static WriteResult<WriteAccepted> Refused(WriteErrorKind kind, string message) =>
        WriteResults.Failure<WriteAccepted>(new WriteError(kind, null, message, [], []));
}
