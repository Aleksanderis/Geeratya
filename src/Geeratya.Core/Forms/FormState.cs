using Geeratya.Jira.Create.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// An open form of one tab (<c>TabState.Form</c>). All of it is plain data in the state layer: the renderer only
/// draws it, and a draft never belongs to a view.
/// </summary>
/// <param name="Target">What the form is for.</param>
/// <param name="Fields">The fields in the order Jira lists them; empty while the metadata loads.</param>
/// <param name="Focus">The index of the field that takes the keys.</param>
/// <param name="IsLoading">The metadata is being fetched.</param>
/// <param name="RequestId">The only metadata request whose reply this form accepts.</param>
/// <param name="TopError">A message that belongs to no field (a load failure, a refused write), or null.</param>
/// <param name="OperationId">The write of the submit while it runs; the form shows Saving and ignores edits.</param>
/// <param name="DiscardPending">Esc was pressed on a form with changes; waiting for y or n.</param>
/// <param name="BrowserUrl">The issue page, shown with the fields the form cannot edit; null when unknown.</param>
public sealed record FormState(
    FormTarget Target,
    IReadOnlyList<FormField> Fields,
    int Focus,
    bool IsLoading,
    long RequestId,
    string? TopError,
    long? OperationId,
    bool DiscardPending,
    string? BrowserUrl)
{
    /// <summary>
    /// The issue types the create form offers while the user has not chosen one (the form then shows the type picker
    /// and no fields); null otherwise.
    /// </summary>
    public IReadOnlyList<CreateIssueType>? TypeChoices { get; init; }

    /// <summary>The highlighted type of <see cref="TypeChoices"/>.</summary>
    public int TypeCursor { get; init; }

    /// <summary>True while the create form asks for the issue type.</summary>
    public bool IsChoosingType => TypeChoices is not null;

    /// <summary>The form waits for the reply of a write.</summary>
    public bool IsSaving => OperationId is not null;

    /// <summary>True when any editable field differs from the value the form was opened with.</summary>
    public bool IsDirty => Fields.Any(candidate => candidate.IsChanged);

    public static FormState Opened(FormTarget target, long requestId, string? browserUrl)
    {
        return new FormState(target, [], 0, true, requestId, null, null, false, browserUrl);
    }
}
