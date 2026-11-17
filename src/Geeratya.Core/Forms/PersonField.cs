using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The behaviour of a person field (Assignee, Reporter, a custom user picker), the same in every form that holds one
/// (create, assign dropdown, boxed edit, transition fields): its quick rows and its search source follow the field and
/// the context of the form, never the form's target type. Quick rows: "Me" unless the account is already chosen,
/// "Unassigned" when the field has a value and may be cleared (it is not required). The search source is the people who
/// can be assigned (Assignee) or who can see the issues (other fields), looked up through the issue key when the
/// issue exists, through the project key while it is created. Pure.
/// </summary>
internal static class PersonField
{
    /// <summary>Whether the field is a person field the user can edit now.</summary>
    public static bool Applies(FormState form, FormField field)
    {
        return field.Kind == FormFieldKind.User && field.IsEditable && !form.Navigating && !form.IsSaving;
    }

    /// <summary>The quick rows of the focused person field while its buffer is empty, or null when no row applies.</summary>
    public static SuggestionState? QuickPopup(AppState state, FormState form)
    {
        if (form.Focus >= form.Fields.Count || !Applies(form, form.Fields[form.Focus]))
        {
            return null;
        }

        var field = form.Fields[form.Focus];
        var rows = new List<Suggestion>();

        if (state.Session.Connection is Connected { AccountId: { } accountId } connected
            && !field.Value.Chosen.Contains(accountId))
        {
            rows.Add(AssigneeQuickRows.Me(accountId, connected.DisplayName));
        }

        if (field.Value.Chosen.Count > 0 && !field.Required)
        {
            rows.Add(AssigneeQuickRows.Unassigned);
        }

        if (rows.Count == 0 || Source(form.Target, field) is not { } source)
        {
            return null;
        }

        return new SuggestionState(form.Focus, source, string.Empty, [], rows, -1, SuggestionStatus.Loaded, 0, null);
    }

    /// <summary>The people search of a person field in the context of <paramref name="target"/>; null with no context.</summary>
    public static SuggestionSource? Source(FormTarget target, FormField field)
    {
        var assignable = field.Id == FormFieldIds.Assignee;

        if (target.IssueKey is { } key)
        {
            var kind = assignable ? SuggestionKind.AssignableUsers : SuggestionKind.IssueUsers;

            return new SuggestionSource(kind, key, []);
        }

        if (target.SearchProjectKey is { } project)
        {
            var kind = assignable ? SuggestionKind.AssignableUsers : SuggestionKind.ProjectUsers;

            return new SuggestionSource(kind, null, [], project);
        }

        return null;
    }

    /// <summary>
    /// The create form's Reporter starts as the signed-in account, which is what Jira sets when none is sent; as the
    /// initial value it is not part of the write unless the user changes it.
    /// </summary>
    public static FormField Prefilled(FormField field, ConnectionState connection)
    {
        if (field is not { Id: FormFieldIds.Reporter, Kind: FormFieldKind.User } || !field.Value.IsEmpty
            || connection is not Connected { AccountId: { } accountId } connected)
        {
            return field;
        }

        var known = new FieldOption(accountId, connected.DisplayName);

        return (field with { Options = [.. field.Options, known] }).WithInitial(FormValue.OfChosen([accountId]));
    }
}
