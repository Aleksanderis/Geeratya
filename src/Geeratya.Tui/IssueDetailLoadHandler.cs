using Geeratya.Core.Cache;
using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Jql;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Tui;

/// <summary>
/// Turns the issue detail effects into the commands that report them. Each load runs on the
/// <see cref="NetworkRunner"/> under its tab's scope (closing the tab cancels it, a reply of an older site is dropped
/// before it is posted) and carries the effect's request id, so the reducer applies only the current one.
/// </summary>
public sealed class IssueDetailLoadHandler
{
    private const string NoConnectionMessage =
        "Not connected to Jira: check the profile and its API token (:profile), then reload.";

    private readonly NetworkRunner _runner;
    private readonly TimeProvider _clock;

    public IssueDetailLoadHandler(NetworkRunner runner, TimeProvider? clock = null)
    {
        _runner = runner;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Starts the load of <paramref name="effect"/> and returns the task that ends when it is posted or dropped.
    /// </summary>
    public async Task StartAsync(Effect effect, SiteCache? cache = null)
    {
        if (cache is not null && effect is LoadIssueDetailEffect opening)
        {
            // Off the UI thread and without a connection; posted before the network reply or failure.
            await _runner.StartLocal(opening.Tab.Value, token => ReadCache(opening, cache, token));
        }

        await StartNetwork(effect);
    }

    private static IssueDetailCached? ReadCache(LoadIssueDetailEffect effect, SiteCache cache, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return null;
        }

        var cached = cache.GetIssue(effect.Key);

        return cached is null ? null : new IssueDetailCached(effect.Tab, effect.RequestId, cached);
    }

    private Task StartNetwork(Effect effect)
    {
        return effect switch
        {
            LoadIssueDetailEffect issue => Run(issue.Tab.Value, issue.Tab, issue.RequestId, IssueAsync(issue)),
            LoadIssueCommentsEffect comments =>
                Run(comments.Tab.Value, comments.Tab, comments.RequestId, CommentsAsync(comments)),
            LoadRemoteLinksEffect links => Run(links.Tab.Value, links.Tab, links.RequestId, LinksAsync(links)),
            LoadIssueChildrenEffect children =>
                Run(children.Tab.Value, children.Tab, children.RequestId, ChildrenAsync(children)),
            _ => Task.CompletedTask,
        };
    }

    private Task Run(
        long scope,
        TabId tab,
        long requestId,
        Func<JiraServices, CancellationToken, Task<Command?>> work)
    {
        return _runner.Start(scope, work, () => Failure(tab, requestId, NoConnectionMessage));
    }

    private Func<JiraServices, CancellationToken, Task<Command?>> IssueAsync(LoadIssueDetailEffect effect)
    {
        return async (services, token) =>
        {
            var result = await services.Issues.GetIssueAsync(effect.Key, token);

            return result.IsSuccess
                ? new IssueDetailLoaded(effect.Tab, effect.RequestId, result.Value, _clock.GetUtcNow())
                : Failure(effect.Tab, effect.RequestId, result.Error);
        };
    }

    private Func<JiraServices, CancellationToken, Task<Command?>> CommentsAsync(LoadIssueCommentsEffect effect)
    {
        return async (services, token) =>
        {
            var result = await services.Comments.GetCommentsAsync(
                effect.Key,
                effect.StartAt,
                token,
                DetailPaging.CommentPageSize);

            return result.IsSuccess
                ? new IssueCommentsLoaded(effect.Tab, effect.RequestId, result.Value, _clock.GetUtcNow())
                : Failure(effect.Tab, effect.RequestId, result.Error);
        };
    }

    private Func<JiraServices, CancellationToken, Task<Command?>> LinksAsync(LoadRemoteLinksEffect effect)
    {
        return async (services, token) =>
        {
            var result = await services.RemoteLinks.GetRemoteLinksAsync(effect.Key, token);

            return result.IsSuccess
                ? new IssueRemoteLinksLoaded(effect.Tab, effect.RequestId, result.Value, _clock.GetUtcNow())
                : Failure(effect.Tab, effect.RequestId, result.Error);
        };
    }

    private Func<JiraServices, CancellationToken, Task<Command?>> ChildrenAsync(LoadIssueChildrenEffect effect)
    {
        return async (services, token) =>
        {
            var jql = JqlBuilder.WithOrder(JqlBuilder.Parent(effect.Key), JqlOrder.RankAscending);
            var query = new IssueSearchQuery(jql);
            var result = await services.Search.SearchAsync(query, null, token);

            if (!result.IsSuccess)
            {
                return Failure(effect.Tab, effect.RequestId, result.Error);
            }

            return new IssueChildrenLoaded(effect.Tab, effect.RequestId, result.Value, _clock.GetUtcNow());
        };
    }

    private static IssueDetailFailed Failure(TabId tab, long requestId, string message)
    {
        return Failure(tab, requestId, new JiraError(JiraErrorKind.Unexpected, null, message));
    }

    private static IssueDetailFailed Failure(TabId tab, long requestId, JiraError error)
    {
        return new IssueDetailFailed(tab, requestId, error);
    }
}
