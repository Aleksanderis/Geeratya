using Geeratya.Core.State;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Views;

public static partial class ViewReducer
{
    /// <summary>
    /// The clock tick: ends a load of the active tab that has waited past <see cref="LoadDeadline.Limit"/>, else
    /// refreshes the active tab in the background when its schedule says so. Returns the same state instance and no
    /// effects when nothing is due, which is almost every tick (a load first seen is stamped once).
    /// </summary>
    private static ReduceResult Ticked(AppState state, DateTimeOffset now)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is not null && LoadDeadline.Apply(state, tab, now) is var aged && !ReferenceEquals(aged, state))
        {
            return new ReduceResult(aged, _noEffects);
        }

        // Writes first: a refresh under a running write would replace rows the write is about to settle. The due
        // time stays in the past, so the first tick after the last operation ends refreshes.
        if (state.Session.Operations.Count > 0 || tab is null || !IsDue(state.Session.Link, tab, now))
        {
            return new ReduceResult(state, _noEffects);
        }

        var effects = new List<Effect>(1);

        return new ReduceResult(StartRefresh(state, tab, true, effects), effects);
    }

    /// <summary>
    /// An automatic refresh is due when the tab is idle (or backing off after a failed load), its schedule has
    /// passed, the link allows traffic and the rows are a first page that can be replaced as a whole.
    /// </summary>
    private static bool IsDue(SiteLink link, TabState tab, DateTimeOffset now)
    {
        if (tab.Cold || tab.Load is Loading || tab.Load is Failed { Reason: LoadReason.NextPage })
        {
            return false;
        }

        if (tab.NextRefreshAt is not { } due || now < due)
        {
            return false;
        }

        var paused = link is Rejected || (link is RateLimited limited && limited.Until > now);

        return !paused && !HasMoreThanFirstPage(tab);
    }

    /// <summary>
    /// A list tab that loaded further pages is not refreshed on its own: tokens only go forward, so replacing the
    /// rows would throw the reader's position away every minute. The manual key resets it.
    /// </summary>
    private static bool HasMoreThanFirstPage(TabState tab)
    {
        return tab.Definition.Source is ViewSourceKind.Jql or ViewSourceKind.SavedFilter
            && tab.Rows.Count > ViewPaging.PageSize;
    }

    internal static AppState StartRefresh(AppState state, TabState tab, bool background, List<Effect> effects)
    {
        var requestId = state.Sequence + 1;

        // A board tab whose configuration never arrived (the open failed on it) fetches it with this refresh, which
        // only a user-lane refresh does, so the clock's refresh is promoted to that lane.
        var needsConfiguration = tab.Board is null && LoadsAllPages(tab.Definition.Source);
        background = background && !needsConfiguration;

        var loading = tab with
        {
            Load = new Loading(LoadReason.Refresh, background),
            RequestId = requestId,
            PendingRows = null,
        };

        effects.Add(
            new LoadViewPageEffect(
                tab.Id,
                tab.Definition,
                null,
                requestId,
                LoadReason.Refresh,
                background,
                QuickFilterJql.For(tab)));

        return Replace(state, loading) with { Sequence = requestId };
    }

    /// <summary>
    /// The refresh key: user lane; it replaces a load in flight. A failed next page is retried as a
    /// next page (the rows stay and the reader keeps the position).
    /// </summary>
    private static AppState Refresh(AppState state, TabId id, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, id);

        // A load in flight does not block the key: a new request id makes the old reply harmless, and a reply that
        // never comes must not leave the tab without a way out (#924).
        if (tab is null || tab.Cold)
        {
            return state;
        }

        if (tab.Load is Failed { Reason: LoadReason.NextPage } && tab.Rows.Count > 0 && tab.Next is not null)
        {
            return RequestNext(state, tab, effects);
        }

        if (state.Session.Operations.Count > 0)
        {
            // Not dropped: it runs when the last write ends (WriteReducer).
            return Replace(state, tab with { RefreshQueued = true }) with { Message = RefreshQueuedText };
        }

        return StartRefresh(state, tab, false, effects);
    }

    /// <summary>
    /// Refreshes the active tab (the <c>view.refresh</c> key); without one the status line says so.
    /// </summary>
    public static ReduceResult RefreshActive(AppState state)
    {
        if (state.Session.Active is not { } id)
        {
            return new ReduceResult(state with { Message = "No view to refresh." }, _noEffects);
        }

        return Reduce(state, new RefreshRequested(id));
    }

    /// <summary>A rate-limit state from the transport; "online" clears only a rate-limit state.</summary>
    private static AppState ChangeLink(AppState state, SiteLink link)
    {
        if (link is Online && state.Session.Link is not RateLimited)
        {
            return state;
        }

        // A rate-limit wait does not hide a rejected login: it would be forgotten when the wait ends.
        if (link is RateLimited && state.Session.Link is Rejected)
        {
            return state;
        }

        return WithLink(state, link);
    }

    private static AppState WithLink(AppState state, SiteLink link)
    {
        return state with { Session = state.Session with { Link = link } };
    }

    /// <summary>What a failed reply says about the link: a 401 rejects it, an unreachable site is offline.</summary>
    private static SiteLink LinkAfterFailure(SiteLink link, JiraErrorKind kind, DateTimeOffset at)
    {
        return kind switch
        {
            JiraErrorKind.Unauthorized => new Rejected(),
            JiraErrorKind.Network or JiraErrorKind.Timeout or JiraErrorKind.HostRefused =>
                link is Offline ? link : new Offline(at),
            _ => link,
        };
    }
}
