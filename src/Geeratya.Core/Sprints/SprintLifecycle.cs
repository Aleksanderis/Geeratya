using Geeratya.Core.Forms;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// <c>sprint.start</c> and <c>sprint.complete</c> (#186): find the sprint like the edit does, count its issues, ask
/// for what is missing (dates, or where the open issues go) in a form, then the write asks its Confirm question. Both
/// work on a Scrum backlog and on a Sprint view. Pure.
/// </summary>
public static class SprintLifecycle
{
    private const string NotFutureText = "Only a future sprint can be started.";
    private const string NotActiveText = "Only an active sprint can be completed.";
    private const string NotLoadedText =
        "Not all issues of the sprint are loaded yet: wait for the load to end, then complete it.";

    private static readonly TimeSpan _defaultLength = TimeSpan.FromDays(14);

    /// <summary><c>sprint.start</c>: asks for missing dates, then confirms.</summary>
    public static ReduceResult Start(AppState state, DateTimeOffset at)
    {
        var found = Find(state);

        if (found.Sprint is not { } sprint)
        {
            return SprintForms.Say(state, found.Reason);
        }

        if (sprint.State != SprintState.Future)
        {
            return SprintForms.Say(state, Refusal(sprint, NotFutureText));
        }

        var tab = found.Tab!;
        var tally = SprintTally.Of(tab, sprint);

        if (sprint.StartDate is { } start && sprint.EndDate is { } end && end > start)
        {
            var request = new StartSprintRequest(
                found.Board,
                sprint,
                start,
                end,
                tally.Count,
                tally.Points,
                !tally.IsComplete);

            return WriteReducer.Reduce(state, new WriteRequested(tab.Id, request, at));
        }

        var from = sprint.StartDate ?? Minute(at);
        var until = sprint.EndDate is { } kept && kept > from ? kept : from + _defaultLength;
        var target = new StartSprintFormTarget(found.Board, sprint, tally);

        return FormReducer.OpenReady(state, tab.Id, target, StartSprintFormTarget.Fields(from, until));
    }

    /// <summary>
    /// <c>sprint.complete</c>: asks where the open issues go (when there is a choice), then confirms.
    /// </summary>
    public static ReduceResult Complete(AppState state, DateTimeOffset at)
    {
        var found = Find(state);

        if (found.Sprint is not { } sprint)
        {
            return SprintForms.Say(state, found.Reason);
        }

        if (sprint.State != SprintState.Active)
        {
            return SprintForms.Say(state, Refusal(sprint, NotActiveText));
        }

        var tab = found.Tab!;
        var tally = SprintTally.Of(tab, sprint);

        if (!tally.IsComplete)
        {
            return SprintForms.Say(state, NotLoadedText);
        }

        var next = tab.Sprints
            .Where(candidate => candidate.State == SprintState.Future)
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefault();

        if (tally.OpenKeys.Count > 0 && next is not null)
        {
            var target = new CompleteSprintFormTarget(found.Board, sprint, tally, next);

            return FormReducer.OpenReady(state, tab.Id, target, target.Fields());
        }

        var request = new CompleteSprintRequest(found.Board, sprint, tally.OpenKeys, tally.DoneCount, null);

        return WriteReducer.Reduce(state, new WriteRequested(tab.Id, request, at));
    }

    private static Found Find(AppState state)
    {
        if (SprintForms.ScrumTab(state) is not { } tab || SprintBoards.BoardOf(tab.Definition) is not { } board)
        {
            return new Found(null, 0, null, SprintForms.NotScrumText);
        }

        if (tab.Detail.Count > 0)
        {
            return new Found(null, 0, null, "Close the issue first (Esc), then start or complete the sprint.");
        }

        var lookup = SprintForms.SprintOf(tab);

        return new Found(tab, board, lookup.Sprint, lookup.Reason);
    }

    private static string Refusal(Sprint sprint, string otherwise)
    {
        return sprint.State == SprintState.Closed ? ClosedSprintGate.Refusal : otherwise;
    }

    private static DateTimeOffset Minute(DateTimeOffset moment)
    {
        return new DateTimeOffset(moment.Ticks - moment.Ticks % TimeSpan.TicksPerMinute, moment.Offset);
    }

    private sealed record Found(TabState? Tab, long Board, Sprint? Sprint, string Reason);
}
