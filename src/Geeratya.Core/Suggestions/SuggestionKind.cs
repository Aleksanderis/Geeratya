namespace Geeratya.Core.Suggestions;

/// <summary>Where a popup gets its candidates; each kind maps to one lookup of the Jira layer.</summary>
public enum SuggestionKind
{
    /// <summary>Labels: those already on visible issues, then the site's labels. New ones are allowed.</summary>
    Labels,

    /// <summary>People who can be assigned the issue.</summary>
    AssignableUsers,

    /// <summary>People who can see the issue (any other user field).</summary>
    IssueUsers,

    /// <summary>Issues by key or text (the parent field).</summary>
    Issues,

    /// <summary>People who can see issues of a project (a person field other than the assignee in a create form).</summary>
    ProjectUsers,

    /// <summary>Components of a project (a create form's components field).</summary>
    Components,

    /// <summary>Versions of a project (a create form's version fields).</summary>
    Versions,

    /// <summary>The allowed values of a select or multiple-choice field: matched locally, no Jira call.</summary>
    Options,
}
