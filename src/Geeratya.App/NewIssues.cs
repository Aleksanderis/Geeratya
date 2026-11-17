using Geeratya.Core.Forms;
using Geeratya.Core.Writes;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Edit.Models;

namespace Geeratya.App;

/// <summary>
/// Turns the filled fields of a create form into the typed <see cref="NewIssue"/> of the Jira layer. The summary,
/// the description and the parent have their own slots of Jira's create call; every other field goes through
/// <see cref="FieldConverter"/>, the converter the edit uses.
/// </summary>
public static class NewIssues
{
    public static NewIssue For(CreateRequest request)
    {
        string? description = null;
        var fields = new Dictionary<string, FieldUpdate>();

        foreach (var change in request.Changes.Where(change => !change.Value.IsEmpty))
        {
            // The parent has its own slot of the request, set from the form.
            if (change.FieldId is FormFieldIds.Summary or FormFieldIds.Parent)
            {
                continue;
            }

            if (change.FieldId == FormFieldIds.Description)
            {
                description = change.Value.Text;
            }
            else
            {
                fields[change.FieldId] = FieldConverter.ForCreate(change);
            }
        }

        return new NewIssue(
            request.ProjectKey,
            request.IssueTypeId,
            request.Summary,
            description,
            request.ParentKey,
            fields,
            DescriptionIsMarkdown: true);
    }
}
