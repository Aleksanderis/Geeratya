using Geeratya.Core.Backlog;
using Geeratya.Core.Forms;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// Opens the sprint form for <c>sprint.create</c> and <c>sprint.edit</c> (#185). Both work on a Scrum backlog and on
/// a Sprint view, the two views that know their board is a Scrum board; every other view answers with a note.
/// Pure.
/// </summary>
public static class SprintForms
{
    public const string NotScrumText = "Sprints exist on Scrum boards: open the Scrum backlog or a sprint first.";

    private const string NoSprintText = "No sprint here to edit.";
    private const string OnBacklogText = "The cursor is on the backlog: move it to an issue of a sprint to edit it.";
    private const string NotLoadedText = "The sprint's details are not loaded yet.";
    private const string DetailText = "Close the issue first (Esc), then edit the sprint.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary><c>sprint.create</c>: a future sprint on the board of the active tab.</summary>
    public static ReduceResult Create(AppState state)
    {
        if (ScrumTab(state) is not { } tab || SprintBoards.BoardOf(tab.Definition) is not { } board)
        {
            return Say(state, NotScrumText);
        }

        var target = new SprintFormTarget(board, null);

        return FormReducer.OpenReady(state, tab.Id, target, SprintFormFields.From(null));
    }

    /// <summary>
    /// <c>sprint.edit</c>: the sprint of the Sprint view, or the sprint whose section holds the cursor in a Scrum
    /// backlog. A closed sprint is read only.
    /// </summary>
    public static ReduceResult Edit(AppState state)
    {
        if (ScrumTab(state) is not { } tab || SprintBoards.BoardOf(tab.Definition) is not { } board)
        {
            return Say(state, NotScrumText);
        }

        if (tab.Detail.Count > 0)
        {
            return Say(state, DetailText);
        }

        var found = SprintOf(tab);

        if (found.Sprint is not { } sprint)
        {
            return Say(state, found.Reason);
        }

        if (sprint.State == SprintState.Closed)
        {
            return Say(state, ClosedSprintGate.Refusal);
        }

        var target = new SprintFormTarget(board, sprint);

        return FormReducer.OpenReady(state, tab.Id, target, SprintFormFields.From(sprint));
    }

    private static TabState? ScrumTab(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return tab is { Definition.Source: ViewSourceKind.ScrumBacklog or ViewSourceKind.Sprint } ? tab : null;
    }

    private static SprintLookup SprintOf(TabState tab)
    {
        if (tab.Definition.Source == ViewSourceKind.Sprint)
        {
            return tab.Sprint is { } shown ? new SprintLookup(shown, string.Empty) : new SprintLookup(null, NoSprintText);
        }

        var section = BacklogSections.Build(tab).SectionOfRow(tab.Selected);

        if (section is null)
        {
            return new SprintLookup(null, NoSprintText);
        }

        if (section.IsBacklog)
        {
            return new SprintLookup(null, OnBacklogText);
        }

        return section.Sprint is { } sprint ? new SprintLookup(sprint, string.Empty) : new SprintLookup(null, NotLoadedText);
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }
}
