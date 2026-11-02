using Geeratya.Core.Forms;
using Geeratya.Core.Writes;
using Geeratya.Jira.Issues.Models;

namespace Geeratya.Core.Links;

/// <summary>The form that links <paramref name="Key"/> to another issue (<c>issue.link</c>, #190).</summary>
public sealed record LinkFormTarget(string Key) : FormTarget
{
    public override string Title => $"Link {Key}";

    public override string IssueKey => Key;

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        var chosen = changes.FirstOrDefault(change => change.FieldId == LinkFormFields.TypeFieldId)?.Value.Chosen;
        var other = changes.FirstOrDefault(change => change.FieldId == LinkFormFields.IssueFieldId)?.Value.Text;
        var choice = chosen is [var id] && LinkOptionId.TryParse(id, out var parsed) ? parsed : null;

        // A missing type goes on as an empty id, which the client refuses before anything is sent.
        return new LinkIssuesRequest(
            Key,
            (other ?? string.Empty).Trim().ToUpperInvariant(),
            choice?.TypeId ?? string.Empty,
            choice?.Direction ?? IssueLinkDirection.Outward,
            choice?.Text ?? string.Empty);
    }
}
