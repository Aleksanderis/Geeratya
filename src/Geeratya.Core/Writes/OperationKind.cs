namespace Geeratya.Core.Writes;

/// <summary>What a running operation does (<c>docs/architecture.md</c>, "Running operations").</summary>
public enum OperationKind
{
    Transition,
    Rank,
    Assign,
    EditFields,
    AddComment,
    EditComment,
    DeleteComment,
    CreateIssue,
    DeleteIssue,
    Bulk,
    LinkIssues,
    UnlinkIssues,
}
