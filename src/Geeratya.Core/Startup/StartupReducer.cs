using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Startup;

/// <summary>
/// What the session shows first (<c>docs/ux-workflows.md</c>, workflow 0): the tabs of the last run, read from the
/// profile's file after the first frame, or, when none are remembered, the board picker (the board itself when the
/// site has one). Also the saving of the tabs as they change. Pure; the host reads and writes the file.
/// </summary>
public static class StartupReducer
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>
    /// Starts the start of a fresh session of the active profile: reads the remembered tabs, or, with restoring
    /// switched off, waits for the connection to open the board picker. A result with no profile is returned as is.
    /// </summary>
    public static ReduceResult Begin(ReduceResult result)
    {
        var state = result.State;

        if (state.Profiles.Active is not { } profile)
        {
            return result;
        }

        if (!state.Settings.RestoreTabs)
        {
            return new ReduceResult(WithPhase(state, StartupPhase.AwaitingConnection), result.Effects);
        }

        return new ReduceResult(WithPhase(state, StartupPhase.Loading), [.. result.Effects, new LoadTabsEffect(profile)]);
    }

    /// <summary>The remembered tabs arrived: reopen them, or prepare the first-start screen.</summary>
    public static ReduceResult Loaded(AppState state, TabsLoaded loaded)
    {
        if (loaded.Profile != state.Profiles.Active || state.Session.Startup != StartupPhase.Loading)
        {
            return Plain(state);
        }

        if (state.Session.Tabs.Count > 0)
        {
            return Plain(WithPhase(state, StartupPhase.Done));
        }

        var (tabs, active) = loaded.Tabs.ToRestored();

        if (tabs.Count == 0)
        {
            var waiting = WithPhase(state, StartupPhase.AwaitingConnection);

            return waiting.Session.Connection is Connected ? FirstStart(waiting) : Plain(waiting);
        }

        var effects = new List<Effect>();
        var restored = ViewReducer.Restore(WithPhase(state, StartupPhase.Done), tabs, active, effects);

        return new ReduceResult(restored, effects);
    }

    /// <summary>The credentials work: a session that restored nothing now opens the board picker.</summary>
    public static ReduceResult Connected(ReduceResult result)
    {
        return result.State.Session.Startup == StartupPhase.AwaitingConnection
            ? Combine(result, FirstStart(result.State))
            : result;
    }

    /// <summary>
    /// A restored tab whose source is gone (404) is dropped with a note; when no tab is left the first-start screen
    /// opens instead. Other failures keep the tab with its error body, as for any tab.
    /// </summary>
    public static ReduceResult AfterLoadFailed(AppState before, ViewLoadFailed failed, ReduceResult result)
    {
        var tab = before.Session.Tabs.FirstOrDefault(candidate => candidate.Id == failed.Tab);

        if (tab is not { Restored: true, LoadedAt: null, Load: Loading { Reason: LoadReason.Open } }
            || tab.RequestId != failed.RequestId || failed.Error.Kind != JiraErrorKind.NotFound)
        {
            return result;
        }

        var closed = Combine(result, ViewReducer.Reduce(result.State, new TabClosed(tab.Id)));
        var note = $"\"{tab.Definition.Title}\" no longer exists and was dropped from the restored tabs.";

        if (closed.State.Session.Tabs.Count > 0)
        {
            return Noted(closed, note);
        }

        return Noted(Combine(closed, FirstStart(closed.State)), note);
    }

    /// <summary>
    /// Adds the save of the tabs when they changed in this step and the session may save them (its start is past
    /// reading the file, and restoring is on).
    /// </summary>
    public static ReduceResult Persisted(AppState before, ReduceResult result)
    {
        var after = result.State;

        if (after.Session.Startup is not (StartupPhase.AwaitingConnection or StartupPhase.Done)
            || before.Session.Startup == StartupPhase.Loading || !after.Settings.RestoreTabs
            || after.Profiles.Active is not { } profile
            || (ReferenceEquals(before.Session.Tabs, after.Session.Tabs) && before.Session.Active == after.Session.Active))
        {
            return result;
        }

        var current = RememberedTabs.Of(after.Session);

        if (current.SameAs(RememberedTabs.Of(before.Session)))
        {
            return result;
        }

        return new ReduceResult(after, [.. result.Effects, new SaveTabsEffect(profile, current)]);
    }

    private static ReduceResult FirstStart(AppState state)
    {
        var done = WithPhase(state, StartupPhase.Done);

        if (done.Session.Tabs.Count > 0 || done.Mode != InputMode.Normal || done.Profiles.Active is null)
        {
            return Plain(done);
        }

        return SourceReducer.OpenFirstBoard(done);
    }

    private static ReduceResult Noted(ReduceResult result, string note)
    {
        var state = result.State with { Message = note, MessageSeverity = StatusSeverity.Warning };

        return new ReduceResult(state, result.Effects);
    }

    private static ReduceResult Combine(ReduceResult first, ReduceResult second)
    {
        return new ReduceResult(second.State, [.. first.Effects, .. second.Effects]);
    }

    private static AppState WithPhase(AppState state, StartupPhase phase)
    {
        return state with { Session = state.Session with { Startup = phase } };
    }

    private static ReduceResult Plain(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }
}
