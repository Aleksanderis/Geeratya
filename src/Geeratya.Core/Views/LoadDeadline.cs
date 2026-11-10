using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Views;

/// <summary>
/// The last resort for a load whose reply never arrives (a throw that was lost, a hung read): on the clock tick a
/// <see cref="Loading"/> older than <see cref="Limit"/> becomes a timeout failure, so the tab or detail offers the
/// refresh key again instead of showing Loading for ever (#924). Pure: the age is measured from the tick that first
/// saw the load, which stamps <see cref="Loading.Since"/>.
/// </summary>
internal static class LoadDeadline
{
    /// <summary>
    /// Above one attempt chain of the transport's defaults (up to 4 attempts of 30 s, back-off between them), so a
    /// load that is retrying has normally answered by then, with a result or with its own failure. A deliberate
    /// trade-off: a worse chain is cut off (the refresh key tries again). The deadline measures silence: a board
    /// gather that receives a page starts it again, and it does not run while the site link is rate limited.
    /// </summary>
    public static TimeSpan Limit { get; } = TimeSpan.FromSeconds(150);

    /// <summary>
    /// The state after the deadline check of the active tab: the same instance when nothing changed, else with the
    /// loads first seen stamped or the first expired load failed.
    /// </summary>
    public static AppState Apply(AppState state, TabState tab, DateTimeOffset now)
    {
        var stamped = Stamp(tab, now);

        if (!ReferenceEquals(stamped, tab))
        {
            return ViewReducer.Replace(state, stamped);
        }

        if (state.Session.Link is RateLimited)
        {
            return state;
        }

        var error = new JiraError(JiraErrorKind.Timeout, null, "No reply from Jira in time.");

        if (Expired(tab.Load, now))
        {
            return ViewReducer.FailLoad(state, new ViewLoadFailed(tab.Id, tab.RequestId, error, now));
        }

        if (tab.Detail.Count == 0)
        {
            return state;
        }

        foreach (var (load, request) in Loads(tab.Detail[^1]))
        {
            if (Expired(load, now))
            {
                return DetailReducer.LoadFailed(state, new IssueDetailFailed(tab.Id, request, error), []);
            }
        }

        return state;
    }

    private static TabState Stamp(TabState tab, DateTimeOffset now)
    {
        var result = tab;

        if (tab.Load is Loading { Since: null } view)
        {
            result = result with { Load = view with { Since = now } };
        }

        if (tab.Detail.Count == 0)
        {
            return result;
        }

        var top = tab.Detail[^1];
        var stamped = top with
        {
            IssueLoad = Stamped(top.IssueLoad, now),
            CommentsLoad = Stamped(top.CommentsLoad, now),
            LinksLoad = Stamped(top.LinksLoad, now),
            ChildrenLoad = Stamped(top.ChildrenLoad, now),
        };

        if (stamped.IssueLoad == top.IssueLoad
            && stamped.CommentsLoad == top.CommentsLoad
            && stamped.LinksLoad == top.LinksLoad
            && stamped.ChildrenLoad == top.ChildrenLoad)
        {
            return result;
        }

        var stack = tab.Detail.ToArray();

        stack[^1] = stamped;

        return result with { Detail = stack };
    }

    private static LoadState Stamped(LoadState load, DateTimeOffset now)
    {
        return load is Loading { Since: null } loading ? loading with { Since = now } : load;
    }

    private static bool Expired(LoadState load, DateTimeOffset now)
    {
        return load is Loading { Since: { } since } && now - since >= Limit;
    }

    private static IEnumerable<(LoadState Load, long Request)> Loads(DetailState detail)
    {
        yield return (detail.IssueLoad, detail.IssueRequest);
        yield return (detail.CommentsLoad, detail.CommentsRequest);
        yield return (detail.LinksLoad, detail.LinksRequest);
        yield return (detail.ChildrenLoad, detail.ChildrenRequest);
    }
}
