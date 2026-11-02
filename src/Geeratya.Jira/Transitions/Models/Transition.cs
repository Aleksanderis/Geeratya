using Geeratya.Jira.Search.Models;

namespace Geeratya.Jira.Transitions.Models;

/// <summary>
/// A workflow transition available on one issue right now. Ids are per project, so a caller picks by
/// <see cref="Target"/>, never from a stored id. Workflow side effects (post-functions, validators) are invisible
/// to the API: a transition with a screen is the only hint, and the UI confirms before posting it.
/// </summary>
/// <param name="Id">The transition id to post.</param>
/// <param name="Name">The transition name (often the target status name).</param>
/// <param name="Target">The status the issue ends in.</param>
/// <param name="HasScreen">True when the workflow shows a screen for it.</param>
/// <param name="IsGlobal">True when it can be done from every status.</param>
/// <param name="Fields">The fields the screen asks for, required and optional; empty without a screen.</param>
public sealed record Transition(
    string Id,
    string Name,
    IssueStatus Target,
    bool HasScreen,
    bool IsGlobal,
    IReadOnlyList<FieldMeta> Fields)
{
    /// <summary>The fields that must be sent: required and not filled in by Jira.</summary>
    public IEnumerable<FieldMeta> RequiredFields =>
        Fields.Where(meta => meta is { Required: true, HasDefaultValue: false });

    /// <summary>
    /// True when the UI must collect input or confirm: a screen, or a required field without default. A heuristic:
    /// post-functions are not visible, so a plain transition can still fire side effects.
    /// </summary>
    public bool NeedsConfirmation => HasScreen || RequiredFields.Any();
}
