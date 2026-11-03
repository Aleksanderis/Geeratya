using Geeratya.Core.Keymap;
using Geeratya.Core.Links;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Forms;

public static partial class FormReducer
{
    internal static ReduceResult Open(AppState state, TabId tabId, FormTarget target)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, tabId);

        if (tab is null)
        {
            return Result(state);
        }

        var requestId = state.Sequence + 1;
        var form = FormState.Opened(target, requestId, BrowserUrl(state, target));
        var opened = ViewReducer.Replace(state, tab with { Form = form });
        var next = opened with
        {
            Mode = InputMode.Insert,
            Sequence = requestId,
            Message = null,
            PendingKeys = string.Empty,
        };

        return new ReduceResult(next, [new LoadFormMetadataEffect(tabId, requestId, target)]);
    }

    /// <summary>
    /// Opens the people picker at once: there is no metadata to fetch, the people come from a search of the typed
    /// text (the suggestion popup). It starts on the current assignee.
    /// </summary>
    internal static ReduceResult OpenAssign(AppState state, TabId tabId, AssignFormTarget target)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, tabId);

        if (tab is null)
        {
            return Result(state);
        }

        var requestId = state.Sequence + 1;
        var field = AssigneeField(target);
        var opened = FormState.Opened(target, requestId, BrowserUrl(state, target));
        var form = WithQuickRows(state, opened with { Fields = [field], IsLoading = false });
        var next = ViewReducer.Replace(state, tab with { Form = form }) with
        {
            Mode = InputMode.Insert,
            Sequence = requestId,
            Message = null,
            PendingKeys = string.Empty,
        };

        return Result(next);
    }

    /// <summary>
    /// Opens a form whose fields the caller already knows (the sprint form): nothing is fetched and the first
    /// editable field has the focus.
    /// </summary>
    internal static ReduceResult OpenReady(
        AppState state,
        TabId tabId,
        FormTarget target,
        IReadOnlyList<FormField> fields)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, tabId);

        if (tab is null)
        {
            return Result(state);
        }

        var requestId = state.Sequence + 1;
        var opened = FormState.Opened(target, requestId, BrowserUrl(state, target));
        var form = opened with { Fields = fields, IsLoading = false };
        var next = ViewReducer.Replace(state, tab with { Form = form with { Focus = Step(form, 0) } }) with
        {
            Mode = InputMode.Insert,
            Sequence = requestId,
            Message = null,
            PendingKeys = string.Empty,
        };

        return Result(next);
    }

    // The assignee is a person field like any other: typing opens the suggestion popup, which searches the people who
    // can be assigned. Backspace on an empty buffer clears it (unassigned).
    private static FormField AssigneeField(AssignFormTarget target)
    {
        List<FieldOption> known = [];

        if (target.Current is { } assignee)
        {
            known.Add(new FieldOption(assignee.AccountId, assignee.DisplayName));
        }

        var field = FormField.Create(FormFieldIds.Assignee, "Assignee", FormFieldKind.User, false, known);

        return target.Current is { } current ? field.WithInitial(FormValue.OfChosen([current.AccountId])) : field;
    }

    private static FieldChange ChangeOf(FormField field)
    {
        var display = FormFieldKinds.IsChoice(field.Kind) || FormFieldKinds.IsPicked(field.Kind)
            ? field.Value.Chosen.Select(id => LabelOf(field, id)).ToArray()
            : null;

        return new FieldChange(field.Id, field.Kind, field.Value, field.Initial, display);
    }

    private static string LabelOf(FormField field, string id)
    {
        return field.Options.FirstOrDefault(option => option.Id == id)?.Label ?? id;
    }

    private static ReduceResult OpenLabels(AppState state)
    {
        var tab = ActiveTab(state);
        var key = tab is null ? null : TargetKey(state, tab);

        if (tab is null || key is null)
        {
            return Say(state, NoIssueText);
        }

        var labels = LabelsOf(tab, key);

        if (labels is null)
        {
            return Say(state, $"The labels of {key} are not loaded yet");
        }

        var known = new Dictionary<string, FormValue> { [FormFieldIds.Labels] = FormValue.OfChosen(labels) };
        var target = new EditFormTarget(key, known, true, FormFieldKind.Labels);

        return Open(state, tab.Id, target);
    }

    private static ReduceResult OpenLink(AppState state)
    {
        var tab = ActiveTab(state);
        var key = tab is null ? null : TargetKey(state, tab);

        return tab is null || key is null ? Say(state, NoIssueText) : Open(state, tab.Id, new LinkFormTarget(key));
    }

    internal static string? TargetKey(AppState state, TabState tab)
    {
        return tab.Detail.Count > 0 ? tab.Detail[^1].Key : BrowserReducer.SelectedKey(state);
    }

    private static IReadOnlyList<string>? LabelsOf(TabState tab, string key)
    {
        if (tab.Detail.Count > 0)
        {
            var detail = tab.Detail[^1];

            return detail.Issue?.Labels ?? detail.Row?.Labels;
        }

        return tab.Rows.FirstOrDefault(row => string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase))?.Labels;
    }

    private static ReduceResult OpenBrowser(AppState state, FormState form)
    {
        if (form.BrowserUrl is not { } url)
        {
            return Result(state with { Message = NoPageText });
        }

        return BrowserReducer.Request(state, new OpenUrlRequested(url, false, form.Target.IssueKey));
    }

    private static string? BrowserUrl(AppState state, FormTarget target)
    {
        var profile = state.Profiles.Active;

        if (profile is null || !Uri.TryCreate(profile.SiteUrl, UriKind.Absolute, out var site))
        {
            return null;
        }

        var connection = new JiraConnection(site, profile.Email);

        // A create has no issue yet: the link goes to the project, where the browser form has every field.
        if (target is CreateFormTarget create)
        {
            return IssueUrls.BrowseProject(connection, create.ProjectKey)?.AbsoluteUri;
        }

        return target.IssueKey is { } key ? IssueUrls.Browse(connection, key)?.AbsoluteUri : null;
    }
}
