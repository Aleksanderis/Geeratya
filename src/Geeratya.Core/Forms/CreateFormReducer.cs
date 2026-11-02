using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The create part of the form reducer: opens the create form from the selection (project and parent come from the
/// issue shown or selected), runs the issue type picker and toggles "open after". The fields, the validation, the
/// submit and the write are the common ones of <see cref="FormReducer"/>. Pure.
/// </summary>
public static class CreateFormReducer
{
    private const string NoProfileText = "No profile is active.";
    private const string NoIssueText =
        "No issue selected: the project and the parent come from the selected or shown issue";
    private const string SubtaskOfSubtaskText = "A sub-task cannot have sub-tasks";
    private const string SubtaskOfEpicText = "An epic has no sub-tasks: create a child of it with :create";
    private const string NoTypesText = "the project offers no issue types for this";

    /// <summary>
    /// <c>issue.create</c> and <c>issue.create-subtask</c>. A selected epic becomes the parent of the new issue when
    /// its type takes one; a sub-task needs a selected issue that can have sub-tasks.
    /// </summary>
    public static ReduceResult Open(AppState state, bool subtask)
    {
        var tab = FormReducer.ActiveTab(state);

        if (state.Session.ProfileName is null)
        {
            return FormReducer.Say(state, NoProfileText);
        }

        if (tab is null)
        {
            return FormReducer.Say(state, NoIssueText);
        }

        if (ContextOf(tab) is not { } context)
        {
            return FormReducer.Say(state, NoIssueText);
        }

        var level = context.Type.HierarchyLevel;

        if (subtask && (context.Type.IsSubtask || level < 0))
        {
            return FormReducer.Say(state, SubtaskOfSubtaskText);
        }

        if (subtask && level > 0)
        {
            return FormReducer.Say(state, SubtaskOfEpicText);
        }

        var parent = subtask || level > 0 ? context.Key : null;
        var target = new CreateFormTarget(context.ProjectKey, parent, subtask, null);

        return FormReducer.Open(state, tab.Id, target);
    }

    /// <summary>The issue types arrived: the picker opens with those the form may offer.</summary>
    public static ReduceResult TypesLoaded(AppState state, FormTypesLoaded loaded)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, loaded.Tab);

        if (tab?.Form is not { IsLoading: true, Target: CreateFormTarget { IssueType: null } target } form
            || form.RequestId != loaded.RequestId)
        {
            return FormReducer.Result(state);
        }

        var types = loaded.Types.Where(target.Offers).ToArray();

        if (types.Length == 0)
        {
            var empty = form with { IsLoading = false, TopError = $"{target.ProjectKey}: {NoTypesText}" };

            return FormReducer.Result(FormReducer.Replace(state, tab, empty));
        }

        var choosing = form with { IsLoading = false, TypeChoices = types, TypeCursor = 0 };

        return FormReducer.Result(FormReducer.Replace(state, tab, choosing));
    }

    /// <summary>A key while the type picker is open: move, choose or leave. Nothing else reaches the form.</summary>
    public static ReduceResult ChoosingType(AppState state, TabState tab, FormState form, string? action)
    {
        var last = (form.TypeChoices?.Count ?? 1) - 1;

        return action switch
        {
            ActionIds.Cancel => FormReducer.Result(FormReducer.Closed(state, tab)),
            ActionIds.PickerNext => Moved(state, tab, form, Math.Min(last, form.TypeCursor + 1)),
            ActionIds.PickerPrevious => Moved(state, tab, form, Math.Max(0, form.TypeCursor - 1)),
            ActionIds.FormNext => Chosen(state, tab, form),
            _ => FormReducer.Result(state),
        };
    }

    /// <summary><c>Ctrl-o</c> in a create form: open the new issue in the detail once it is created, or not.</summary>
    public static AppState ToggleOpenAfter(AppState state, TabState tab, FormState form)
    {
        if (form.Target is not CreateFormTarget create)
        {
            return state;
        }

        var toggled = form with { Target = create with { OpenAfter = !create.OpenAfter } };

        return FormReducer.Replace(state, tab, toggled);
    }

    private static ReduceResult Moved(AppState state, TabState tab, FormState form, int cursor)
    {
        return FormReducer.Result(FormReducer.Replace(state, tab, form with { TypeCursor = cursor }));
    }

    private static ReduceResult Chosen(AppState state, TabState tab, FormState form)
    {
        if (form is not { Target: CreateFormTarget create, TypeChoices: { Count: > 0 } types })
        {
            return FormReducer.Result(state);
        }

        var target = create with { IssueType = types[Math.Clamp(form.TypeCursor, 0, types.Count - 1)] };
        var requestId = state.Sequence + 1;
        var loading = FormState.Opened(target, requestId, form.BrowserUrl);
        var next = FormReducer.Replace(state, tab, loading) with { Sequence = requestId };

        return new ReduceResult(next, [new LoadFormMetadataEffect(tab.Id, requestId, target)]);
    }

    private static CreateContext? ContextOf(TabState tab)
    {
        if (tab.Detail.Count > 0)
        {
            var shown = tab.Detail[^1];

            if (shown.Issue is { } issue)
            {
                return new CreateContext(issue.Key, issue.ProjectKey, issue.IssueType);
            }

            return shown.Row is { } held ? new CreateContext(held.Key, held.ProjectKey, held.IssueType) : null;
        }

        var row = SelectedRow(tab);

        return row is null ? null : new CreateContext(row.Key, row.ProjectKey, row.IssueType);
    }

    private static IssueSummary? SelectedRow(TabState tab)
    {
        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return ColumnsNavigation.SelectedIssue(tab);
        }

        return tab.Selected >= 0 && tab.Selected < tab.Rows.Count ? tab.Rows[tab.Selected] : null;
    }

    private sealed record CreateContext(string Key, string ProjectKey, IssueTypeSummary Type);
}
