using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Jira.Edit.Models;

/// <summary>
/// A field of one issue that can be edited now (from <c>editmeta</c>). The set depends on the issue's type and
/// status and on the account, so it is fetched when the editor opens. Nothing here is listed in code.
/// </summary>
/// <param name="FieldId">The id to write under (<c>priority</c>, <c>customfield_10021</c>).</param>
/// <param name="Name">The label Jira shows.</param>
/// <param name="Required">True when Jira refuses an empty value.</param>
/// <param name="SchemaType">Jira's schema type (<c>string</c>, <c>array</c>, <c>user</c>); empty when unknown.</param>
/// <param name="SchemaItems">The element type of an array field (<c>option</c>, <c>component</c>); empty otherwise.</param>
/// <param name="Operations">The operations Jira allows: <c>set</c>, <c>add</c>, <c>remove</c>, ...</param>
/// <param name="AllowedValues">The choices Jira lists; empty for free-form and user fields.</param>
/// <param name="HasDefaultValue">True when Jira fills the field in when it is left out.</param>
/// <param name="AutoCompleteUrl">Where Jira says the choices of a picker can be searched; null when absent.</param>
/// <param name="Kind">How the app edits the field; <see cref="EditFieldKind.Unsupported"/> means read-only.</param>
public sealed record EditField(
    string FieldId,
    string Name,
    bool Required,
    string SchemaType,
    string SchemaItems,
    IReadOnlyList<string> Operations,
    IReadOnlyList<FieldOption> AllowedValues,
    bool HasDefaultValue,
    string? AutoCompleteUrl,
    EditFieldKind Kind)
{
    /// <summary>True when the app can write the field and Jira allows at least one operation on it.</summary>
    public bool IsEditable => Kind != EditFieldKind.Unsupported && Operations.Count > 0;
}
