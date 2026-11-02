using Geeratya.Core.Writes;
using Geeratya.Jira.Create.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// Create an issue. The form starts with the type picker (<see cref="IssueType"/> is null) and reloads with the
/// fields of the chosen type. Project and parent come from the selection the user opened it from; whether a type
/// takes a parent is read from its create metadata (a <c>parent</c> field), never from the type's name.
/// </summary>
/// <param name="ProjectKey">The project the issue is created in.</param>
/// <param name="ParentKey">The issue the new one hangs under (a sub-task's parent, an epic's child), or null.</param>
/// <param name="SubtasksOnly">The picker offers only sub-task types (a parent is required); else none.</param>
/// <param name="IssueType">The chosen type, or null while the picker is open.</param>
/// <param name="OpenAfter">Show the new issue in the detail when Jira has created it.</param>
public sealed record CreateFormTarget(
    string ProjectKey,
    string? ParentKey,
    bool SubtasksOnly,
    CreateIssueType? IssueType,
    bool OpenAfter = false) : FormTarget
{
    private const string NeedsParentText =
        "This issue type needs a parent: create it from the parent (:create subtask)";

    public override string Title
    {
        get
        {
            if (IssueType is null)
            {
                return SubtasksOnly ? $"Create sub-task of {ParentKey}" : $"Create issue in {ProjectKey}";
            }

            if (ParentKey is null)
            {
                return $"Create {IssueType.Name} in {ProjectKey}";
            }

            var link = SubtasksOnly ? "of" : "under";

            return $"Create {IssueType.Name} {link} {ParentKey}";
        }
    }

    public override string? IssueKey => null;

    /// <summary>True for a type the picker offers: sub-task types or none, as the form was opened.</summary>
    public bool Offers(CreateIssueType type)
    {
        return type.IsSubtask == SubtasksOnly;
    }

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        var type = IssueType ?? throw new InvalidOperationException("The issue type is not chosen yet.");

        return new CreateRequest(ProjectKey, type.Id, type.Name, ParentKey, changes, OpenAfter);
    }

    public override FormTarget Loaded(IReadOnlyList<FormField> fields)
    {
        var takesParent = fields.Any(field => field.Id == FormFieldIds.Parent);

        return takesParent || ParentKey is null ? this : this with { ParentKey = null };
    }

    public override string? Blocked(IReadOnlyList<FormField> fields)
    {
        var needsParent = fields.Any(field => field is { Id: FormFieldIds.Parent, Required: true });

        return needsParent && ParentKey is null ? NeedsParentText : null;
    }
}
