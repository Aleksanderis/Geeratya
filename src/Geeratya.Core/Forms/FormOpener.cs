using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The entry points that open the edit and assign forms (<c>issue.edit</c>, <c>issue.assign</c>, the palette's
/// <c>:edit</c>, <c>:priority</c> and <c>:assign</c>) on the issue the active tab shows or has selected, and the
/// assign-to-me key. They pass the current values the tab already holds: the editor metadata carries none, and a
/// field with an unknown value would be overwritten blind, so the edit form offers only the known ones. Pure.
/// </summary>
public static class FormOpener
{
    /// <summary>The status text of a description that cannot be edited here.</summary>
    public const string DescriptionBrowserText =
        "This description has formatting that cannot be edited here: open it in the browser (<space>o)";

    private const string DescriptionLoadingText = "The description is still loading";
    private const string NoIssueText = "No issue selected: select a row or open an issue first";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>
    /// <c>issue.edit</c> and <c>:edit</c>: the title pane of the issue in its edit state. From a list or board the
    /// detail overlay opens on the selected issue first (<c>docs/ux-screens.md</c>, "Editing in place").
    /// </summary>
    public static ReduceResult Edit(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        if (tab is null || state.Session.ProfileName is null)
        {
            return Say(state, NoIssueText);
        }

        if (tab.Detail.Count > 0)
        {
            return EditInPane(state, FormFieldIds.Summary);
        }

        var effects = new List<Effect>();
        var opened = DetailReducer.OpenSelected(state, effects);

        if (ViewReducer.Find(opened.Session.Tabs, tab.Id) is not { Detail.Count: > 0 })
        {
            return Say(state, NoIssueText);
        }

        var form = EditInPane(opened, FormFieldIds.Summary);

        return new ReduceResult(form.State, [.. effects, .. form.Effects]);
    }

    /// <summary>Enter on a field of the issue detail: the edit form limited to <paramref name="fieldId"/>.</summary>
    public static ReduceResult EditField(AppState state, string fieldId)
    {
        return Open(state, fieldId);
    }

    /// <summary>
    /// <c>Enter</c> or <c>i</c> on the title or Description pane: the edit state of that pane, saved on its own with
    /// <c>Ctrl-s</c>. A description that does not survive the Markdown-like text stays read-only and says so.
    /// </summary>
    public static ReduceResult EditInPane(AppState state, string fieldId)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        if (fieldId == FormFieldIds.Description && tab is { Detail.Count: > 0 })
        {
            var shown = tab.Detail[^1];

            if (shown.Issue is null)
            {
                return Say(state, DescriptionLoadingText);
            }

            if (!DescriptionEditability.CanEdit(shown.Issue.Description))
            {
                return Say(state, DescriptionBrowserText);
            }
        }

        var opened = Open(state, fieldId);

        if (ViewReducer.Find(opened.State.Session.Tabs, tab?.Id ?? default) is not { Form: { } form } current)
        {
            return opened;
        }

        var pane = fieldId == FormFieldIds.Description ? PaneIds.Description : PaneIds.Title;

        return new ReduceResult(
            ViewReducer.Replace(opened.State, current with { Form = form with { Pane = pane } }),
            opened.Effects);
    }

    /// <summary><c>:priority</c>: the edit form limited to the priority.</summary>
    public static ReduceResult Priority(AppState state)
    {
        return Open(state, FormFieldIds.Priority);
    }

    /// <summary><c>:estimate</c>: the edit form limited to the estimate (story points) field of the issue.</summary>
    public static ReduceResult Estimate(AppState state)
    {
        if (Subject.Of(state) is not { } subject)
        {
            return Say(state, NoIssueText);
        }

        if (subject.Tab.Board is { HasNoEstimation: true })
        {
            return Say(state, $"{subject.Key} has no estimate to edit: the board has no story point estimation");
        }

        var target = new EditFormTarget(
            subject.Key,
            new Dictionary<string, FormValue>(),
            true,
            FormFieldKind.Number,
            EstimateOnly: true,
            EstimateFieldId: subject.Tab.Board?.EstimationFieldId);

        return FormReducer.Open(state, subject.Tab.Id, target);
    }

    /// <summary><c>issue.assign</c>: the people picker (the form with the searchable Assignee field).</summary>
    public static ReduceResult Assign(AppState state)
    {
        if (Subject.Of(state) is not { } subject)
        {
            return Say(state, NoIssueText);
        }

        var target = new AssignFormTarget(subject.Key, subject.Assignee);

        return FormReducer.OpenAssign(state, subject.Tab.Id, target);
    }

    /// <summary><c>issue.assign-me</c>: assigns the shown or selected issue to the account of the profile.</summary>
    public static ReduceResult AssignMe(AppState state, DateTimeOffset at)
    {
        if (Subject.Of(state) is not { } subject)
        {
            return Say(state, NoIssueText);
        }

        return WriteReducer.Reduce(state, new AssignToMeRequested(subject.Tab.Id, subject.Key, at));
    }

    private static ReduceResult Open(AppState state, string onlyId)
    {
        if (Subject.Of(state) is not { } subject)
        {
            return Say(state, NoIssueText);
        }

        var known = new Dictionary<string, FormValue>
        {
            [FormFieldIds.Summary] = FormValue.OfText(subject.Summary),
            [FormFieldIds.Labels] = FormValue.OfChosen(subject.Labels),
        };

        if (subject.PriorityName is { } priority)
        {
            known[FormFieldIds.Priority] = FormValue.OfText(priority);
        }

        known[FormFieldIds.Parent] = subject.ParentKey is { } parent ? FormValue.OfChosen([parent]) : FormValue.Empty;

        var lossy = new List<string>();

        AddDescription(subject, known, lossy);

        var target = new EditFormTarget(subject.Key, known, true, null, onlyId, lossy, ParentLevel: subject.ParentLevel);

        return FormReducer.Open(state, subject.Tab.Id, target);
    }

    // A description is known only in the opened issue. One that does not survive the Markdown-like text would lose
    // content when saved, so it is listed read-only (with the link to the browser) instead.
    private static void AddDescription(Subject subject, Dictionary<string, FormValue> known, List<string> lossy)
    {
        if (!subject.DescriptionKnown)
        {
            return;
        }

        if (subject.Description is not { } document)
        {
            known[FormFieldIds.Description] = FormValue.Empty;

            return;
        }

        if (DescriptionEditability.CanEdit(document))
        {
            known[FormFieldIds.Description] = FormValue.OfText(AdfMarkdownWriter.ToText(document));
        }
        else
        {
            lossy.Add(FormFieldIds.Description);
        }
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }

    private sealed record Subject(
        TabState Tab,
        string Key,
        string Summary,
        IReadOnlyList<string> Labels,
        string? PriorityName,
        IssueAssignee? Assignee,
        string? ParentKey,
        int? ParentLevel,
        bool DescriptionKnown,
        AdfDocument? Description)
    {
        public static Subject? Of(AppState state)
        {
            var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

            if (tab is null || state.Session.ProfileName is null)
            {
                return null;
            }

            if (tab.Detail.Count > 0)
            {
                return OfDetail(tab, tab.Detail[^1]);
            }

            var key = BrowserReducer.SelectedKey(state);
            var row = tab.Rows.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));

            return row is null ? null : OfRow(tab, row);
        }

        private static Subject? OfDetail(TabState tab, DetailState shown)
        {
            if (shown.Issue is { } issue)
            {
                return new Subject(
                    tab,
                    issue.Key,
                    issue.Summary,
                    issue.Labels,
                    issue.Priority,
                    issue.Assignee,
                    issue.Parent?.Key,
                    ParentLevels.Accepted(issue.IssueType.HierarchyLevel, issue.IssueType.IsSubtask),
                    true,
                    issue.Description);
            }

            return shown.Row is { } row ? OfRow(tab, row) : null;
        }

        private static Subject OfRow(TabState tab, IssueSummary row)
        {
            return new Subject(
                tab,
                row.Key,
                row.Summary,
                row.Labels,
                row.Priority,
                row.Assignee,
                row.Parent?.Key,
                ParentLevels.Accepted(row.IssueType.HierarchyLevel, row.IssueType.IsSubtask),
                false,
                null);
        }
    }
}
