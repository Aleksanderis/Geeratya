using Geeratya.Jira.Search.Models;

namespace Geeratya.Jira.Issues.Models;

/// <summary>
/// A small reference to another issue as Jira embeds it (parent, sub-task, linked issue): enough to show a line and
/// open the issue. Status and type are neutral values when Jira omitted them.
/// </summary>
public sealed record IssueStub(string Key, string Summary, IssueStatus Status, IssueTypeSummary IssueType);
