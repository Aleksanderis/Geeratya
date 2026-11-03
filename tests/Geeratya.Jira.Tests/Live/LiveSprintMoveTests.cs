using Geeratya.Jira.Edit;
using Geeratya.Jira.Sprints;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Live;

/// <summary>
/// Moves one throwaway issue into one throwaway future sprint, then to the backlog, and re-reads the issue after
/// each step, on the Scrum sandbox projects (<c>GCS</c> on board 6, <c>GTS</c> on board 5). The issue carries
/// <c>geeratya-test-&lt;run id&gt;</c>; issue and sprint are deleted at the end, also when a step fails.
/// </summary>
public class LiveSprintMoveTests
{
    private const string IssuePath = "/rest/api/3/issue";
    private const string SprintPath = "/rest/agile/1.0/sprint";

    [LiveFact]
    public Task GivenCompanyManagedScrumBoard_WhenMovingIssueToSprintAndBacklog_ThenEachMoveTakesEffect() =>
        RunAsync("GCS", 6);

    [LiveFact]
    public Task GivenTeamManagedScrumBoard_WhenMovingIssueToSprintAndBacklog_ThenEachMoveTakesEffect() =>
        RunAsync("GTS", 5);

    private static async Task RunAsync(string project, long boardId)
    {
        var environment = LiveEnvironment.Load();
        using var transport = environment.CreateTransport();
        var runId = Guid.NewGuid().ToString("N")[..8];
        var token = CancellationToken.None;

        var created = await transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Post, IssuePath, CreateBody(project, runId)),
            LiveWriteJsonContext.Default.LiveCreatedIssue,
            token);
        Assert.True(created.IsSuccess, created.IsSuccess ? string.Empty : created.Error.Message);
        var key = created.Value.Key;

        WriteResult<WriteAccepted>? sprintDeleted = null;
        WriteResult<WriteAccepted> issueDeleted;

        try
        {
            var sprint = await new SprintWriteClient(transport)
                .CreateAsync($"geeratya-test-{runId}", boardId, null, null, null, token);
            Assert.True(sprint.IsSuccess, sprint.IsSuccess ? string.Empty : sprint.Error.Message);

            try
            {
                await StepsAsync(transport, key, sprint.Value.Id, token);
            }
            finally
            {
                sprintDeleted = await transport.SendWriteAsync(
                    JiraRequest.Write(HttpMethod.Delete, $"{SprintPath}/{sprint.Value.Id}", null),
                    token);
            }
        }
        finally
        {
            issueDeleted = await new EditClient(transport).DeleteIssueAsync(key, false, token);
        }

        Assert.True(sprintDeleted.IsSuccess, sprintDeleted.IsSuccess ? string.Empty : sprintDeleted.Error.Message);
        Assert.True(issueDeleted.IsSuccess, issueDeleted.IsSuccess ? string.Empty : issueDeleted.Error.Message);
    }

    private static async Task StepsAsync(JiraTransport transport, string key, long sprintId, CancellationToken token)
    {
        var client = new SprintMoveClient(transport);
        Assert.Null(await SprintOfAsync(transport, key, token));

        var toSprint = await client.MoveToSprintAsync(sprintId, [key], token);
        Assert.True(toSprint.IsSuccess, toSprint.IsSuccess ? string.Empty : toSprint.Error.Message);
        Assert.Equal([key], toSprint.Value.Moved);
        Assert.True(toSprint.Value.AllMoved);
        Assert.Equal(sprintId, await SprintOfAsync(transport, key, token));

        var toBacklog = await client.MoveToBacklogAsync([key], token);
        Assert.True(toBacklog.IsSuccess, toBacklog.IsSuccess ? string.Empty : toBacklog.Error.Message);
        Assert.Equal([key], toBacklog.Value.Moved);
        Assert.True(toBacklog.Value.AllMoved);
        Assert.Null(await SprintOfAsync(transport, key, token));
    }

    private static async Task<long?> SprintOfAsync(JiraTransport transport, string key, CancellationToken token)
    {
        var read = await transport.SendAsync(
            JiraRequest.Get($"/rest/agile/1.0/issue/{key}?fields=sprint"),
            LiveWriteJsonContext.Default.LiveSprintIssue,
            token);
        Assert.True(read.IsSuccess, read.IsSuccess ? string.Empty : read.Error.Message);

        return read.Value.Fields.Sprint?.Id;
    }

    private static string CreateBody(string project, string runId) =>
        $$$"""{"fields":{"project":{"key":"{{{project}}}"},"issuetype":{"name":"Task"},"summary":"geeratya live sprint move","labels":["geeratya-test-{{{runId}}}"]}}""";
}
