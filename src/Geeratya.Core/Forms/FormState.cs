using Geeratya.Core.Suggestions;
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
/// <param name="Suggest">The suggestion popup of the focused field while one is open, else null.</param>
public sealed record FormState(
    FormTarget Target,
    IReadOnlyList<FormField> Fields,
    int Focus,
    bool IsLoading,
    long RequestId,
    string? TopError,
    long? OperationId,
    bool DiscardPending,
    string? BrowserUrl,
    SuggestionState? Suggest = null)
{
    /// <summary>
    /// The issue types the create form offers while the user has not chosen one (the form then shows the type picker
    /// and no fields); null otherwise.
    /// </summary>
    public IReadOnlyList<CreateIssueType>? TypeChoices { get; init; }

    /// <summary>
    /// The detail field this form hangs under as a dropdown (a <c>DetailFieldIds</c> member), null for a form drawn
    /// as its own box. The keys and the write path are the form's; only the drawing differs, and accepting a row
    /// writes at once.
    /// </summary>
    public string? Dropdown { get; init; }

    /// <summary>The highlighted type of <see cref="TypeChoices"/>.</summary>
    public int TypeCursor { get; init; }

    /// <summary>
    /// The form has a navigate state besides editing (the edit form with more than one editable field, #650): in it
    /// <c>j</c>/<c>k</c>/<c>Tab</c> move from field to field and <c>Enter</c>, <c>i</c> or a click starts editing the
    /// focused one; <c>Esc</c> stops editing. Other forms are always editing.
    /// </summary>
    public bool Navigable { get; init; }

    /// <summary>True while a <see cref="Navigable"/> form moves between fields (no field takes text).</summary>
    public bool Navigating { get; init; }

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
