using Geeratya.Jira.Edit;
using Geeratya.Jira.Jql;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Live;

/// <summary>
/// Issue #281: the sub-task JQL and the delete flow of #165 on the team-managed sandbox. The test creates its own
/// parent and sub-task (label <c>geeratya-test-&lt;run id&gt;</c>) and deletes them with the parent at the end.
/// </summary>
public class LiveSubtaskDeleteTests
{
    private const string IssuePath = "/rest/api/3/issue";

    [LiveFact]
    public async Task GivenParentWithSubtask_WhenSearchingAndDeleting_ThenSubtaskJqlFindsItAndDeleteNeedsTheFlag()
    {
        var environment = LiveEnvironment.Load();
        using var transport = environment.CreateTransport();
        var edits = new EditClient(transport);
        var runId = Guid.NewGuid().ToString("N")[..8];
        var token = CancellationToken.None;

        var parent = await CreateAsync(transport, "Task", null, runId, token);

        try
        {
            var child = await CreateAsync(transport, "Subtask", parent, runId, token);

            var search = new JiraSearchClient(transport);
            var found = await search.SearchAsync(new IssueSearchQuery(JqlBuilder.SubTasksOf(parent)), null, token);
            Assert.True(found.IsSuccess);
            Assert.Equal([child], found.Value.Items.Select(item => item.Key));

            var refused = await edits.DeleteIssueAsync(parent, false, token);
            Assert.False(refused.IsSuccess);
            Assert.Equal(WriteErrorKind.Validation, refused.Error.Kind);

            var deleted = await edits.DeleteIssueAsync(parent, true, token);
            Assert.True(deleted.IsSuccess);

            foreach (var key in new[] { parent, child })
            {
                var gone = await transport.SendAsync(
                    JiraRequest.Get($"{IssuePath}/{key}?fields=summary"),
                    LiveWriteJsonContext.Default.LiveIssueState,
                    token);
                Assert.False(gone.IsSuccess);
                Assert.Equal(JiraErrorKind.NotFound, gone.Error.Kind);
            }
        }
        finally
        {
            // Already gone on success (404, ignored); removes the sub-task with the parent after a failed step.
            await edits.DeleteIssueAsync(parent, true, token);
        }
    }

    private static async Task<string> CreateAsync(
        JiraTransport transport,
        string issueType,
        string? parent,
        string runId,
        CancellationToken token)
    {
        var parentPart = parent is null ? string.Empty : $$$""","parent":{"key":"{{{parent}}}"}""";
        var body =
            $$$"""{"fields":{"project":{"key":"GTM"},"issuetype":{"name":"{{{issueType}}}"},"summary":"geeratya live sub-task delete"{{{parentPart}}},"labels":["geeratya-test-{{{runId}}}"]}}""";
        var created = await transport.SendWriteAsync(
            JiraRequest.Write(HttpMethod.Post, IssuePath, body),
            LiveWriteJsonContext.Default.LiveCreatedIssue,
            token);
        Assert.True(created.IsSuccess);

        return created.Value.Key;
    }
}
