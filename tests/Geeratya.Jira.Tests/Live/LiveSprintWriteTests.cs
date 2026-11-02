using Geeratya.Jira.Sprints;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;

namespace Geeratya.Jira.Tests.Live;

/// <summary>
/// Creates, edits, starts and completes one throwaway sprint on each Scrum sandbox board (6 is the company-managed
/// <c>GCS</c>, 5 the team-managed <c>GTS</c>) and deletes it at the end, also when a step fails. Sprint writes are
/// allowed only on these boards (testing skill, "Live dev site").
/// </summary>
public class LiveSprintWriteTests
{
    private const string SprintPath = "/rest/agile/1.0/sprint";

    [LiveFact]
    public Task GivenCompanyManagedScrumBoard_WhenRunningSprintLifecycle_ThenEachStepTakesEffect() =>
        RunLifecycleAsync(6);

    [LiveFact]
    public Task GivenTeamManagedScrumBoard_WhenRunningSprintLifecycle_ThenEachStepTakesEffect() =>
        RunLifecycleAsync(5);

    private static async Task RunLifecycleAsync(long boardId)
    {
        var environment = LiveEnvironment.Load();
        using var transport = environment.CreateTransport();
        var client = new SprintWriteClient(transport);
        var runId = Guid.NewGuid().ToString("N")[..8];
        var token = CancellationToken.None;
        var created = await client.CreateAsync($"geeratya-test-{runId}", boardId, "live goal", null, null, token);
        Assert.True(created.IsSuccess, created.IsSuccess ? string.Empty : created.Error.Message);
        var sprintId = created.Value.Id;

        try
        {
            await StepsAsync(client, sprintId, token);
        }
        finally
        {
            var deleted = await transport.SendWriteAsync(
                JiraRequest.Write(HttpMethod.Delete, $"{SprintPath}/{sprintId}", null),
                token);
            Assert.True(deleted.IsSuccess, deleted.IsSuccess ? string.Empty : deleted.Error.Message);
        }
    }

    private static async Task StepsAsync(SprintWriteClient client, long sprintId, CancellationToken token)
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(7);

        var renamed = await client.UpdateAsync(sprintId, new SprintEdit("geeratya-test-renamed", null, null, null), token);
        Assert.True(renamed.IsSuccess);
        Assert.Equal("live goal", renamed.Value.Goal);

        var cleared = await client.UpdateAsync(sprintId, new SprintEdit(null, "", null, null), token);
        Assert.True(cleared.IsSuccess);
        Assert.Null(cleared.Value.Goal);

        var undated = await client.CompleteAsync(sprintId, token);
        Assert.Equal(SprintWriteProblem.StartDateMissing, SprintWriteErrors.Classify(undated.Error));

        var dated = await client.UpdateAsync(sprintId, new SprintEdit(null, null, start, end), token);
        Assert.True(dated.IsSuccess);

        var early = await client.CompleteAsync(sprintId, token);
        Assert.Equal(SprintWriteProblem.NotActive, SprintWriteErrors.Classify(early.Error));

        var started = await client.StartAsync(sprintId, start, end, token);
        Assert.True(started.IsSuccess);
        Assert.Equal(WrittenSprintState.Active, started.Value.State);

        var completed = await client.CompleteAsync(sprintId, token);
        Assert.True(completed.IsSuccess);
        Assert.Equal(WrittenSprintState.Closed, completed.Value.State);
        Assert.NotNull(completed.Value.CompleteDate);

        var reopened = await client.StartAsync(sprintId, start, end, token);
        Assert.Equal(SprintWriteProblem.AlreadyClosed, SprintWriteErrors.Classify(reopened.Error));
    }
}
