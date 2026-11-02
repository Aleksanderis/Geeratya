namespace Geeratya.Jira.Transitions.Models;

/// <summary>
/// A field a transition screen asks for (the create-metadata shape). Nothing here is listed in code: the field set,
/// its schema and its allowed values come from Jira for the issue and workflow at hand.
/// </summary>
/// <param name="FieldId">The id to post under (<c>resolution</c>, <c>customfield_10010</c>).</param>
/// <param name="Name">The label Jira shows.</param>
/// <param name="Required">True when the transition is refused without a value.</param>
/// <param name="SchemaType">Jira's schema type (<c>resolution</c>, <c>string</c>); empty when unknown.</param>
/// <param name="HasDefaultValue">True when Jira fills the field in when it is left out.</param>
/// <param name="AllowedValues">The choices Jira lists; empty for free-form fields.</param>
/// <param name="SchemaItems">The element type of an array field (<c>option</c>, <c>string</c>); else empty.</param>
/// <param name="SchemaSystem">The system name of a built-in field (<c>labels</c>, <c>description</c>); else empty.</param>
/// <param name="SchemaCustom">The custom field type key (a text area is one); else empty.</param>
public sealed record FieldMeta(
    string FieldId,
    string Name,
    bool Required,
    string SchemaType,
    bool HasDefaultValue,
    IReadOnlyList<FieldOption> AllowedValues,
    string SchemaItems = "",
    string SchemaSystem = "",
    string SchemaCustom = "");
