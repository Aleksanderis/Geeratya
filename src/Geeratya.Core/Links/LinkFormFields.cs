using Geeratya.Core.Forms;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Links;
using Geeratya.Jira.Links.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Links;

/// <summary>The fields of the link form: the type with its side, and the issue at the other end (found by the suggestion popup, or a full key typed). Pure.</summary>
public static class LinkFormFields
{
    public const string TypeFieldId = "linktype";
    public const string IssueFieldId = LinkClient.IssueFieldId;

    private const string TypeLabel = "Link type";
    private const string IssueLabel = "Issue";

    public static IReadOnlyList<FormField> From(IReadOnlyList<IssueLinkType> types)
    {
        var options = new List<FieldOption>();

        foreach (var type in types)
        {
            options.Add(OptionOf(type, IssueLinkDirection.Outward, type.Outward));

            // A type that reads the same both ways ("relates to") is one choice, not two.
            if (type.Inward != type.Outward)
            {
                options.Add(OptionOf(type, IssueLinkDirection.Inward, type.Inward));
            }
        }

        return
        [
            FormField.Create(TypeFieldId, TypeLabel, FormFieldKind.Select, true, options),
            FormField.Create(IssueFieldId, IssueLabel, FormFieldKind.Parent, true, []) with { TakesTypedKey = true },
        ];
    }

    private static FieldOption OptionOf(IssueLinkType type, IssueLinkDirection direction, string text)
    {
        return new FieldOption(LinkOptionId.Of(type.Id, direction, text), text);
    }
}
