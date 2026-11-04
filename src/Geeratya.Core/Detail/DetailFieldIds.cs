using Geeratya.Core.Forms;

namespace Geeratya.Core.Detail;

/// <summary>The ids of the fields the issue detail draws as form rows (<see cref="DetailTargetKind.Field"/>).</summary>
public static class DetailFieldIds
{
    public const string Summary = FormFieldIds.Summary;
    public const string Status = "status";
    public const string Priority = FormFieldIds.Priority;
    public const string Assignee = FormFieldIds.Assignee;
    public const string Labels = FormFieldIds.Labels;
    public const string Reporter = "reporter";
    public const string Parent = "parent";
    public const string Epic = "epic";
    public const string Sprint = "sprint";
    public const string Estimate = "estimate";
    public const string Updated = "updated";
    public const string Attachments = "attachments";
    public const string Description = FormFieldIds.Description;
}
