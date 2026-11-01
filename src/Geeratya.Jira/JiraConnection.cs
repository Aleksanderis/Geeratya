namespace Geeratya.Jira;

/// <summary>Identifies one Jira site. Every Jira call takes one explicitly; there is no global current site.</summary>
public sealed record JiraConnection(Uri SiteUrl);
