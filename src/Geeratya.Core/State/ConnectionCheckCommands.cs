using Geeratya.Core.Profiles;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;

namespace Geeratya.Core.State;

/// <summary>Start the check for the active profile (sent once at start).</summary>
public sealed record ConnectionCheckRequested : Command;

/// <summary>Jira accepted the credentials of <paramref name="Profile"/>. Ignored when it is no longer active.</summary>
public sealed record ConnectionChecked(Profile Profile, JiraUser User) : Command;

/// <summary>The check of <paramref name="Profile"/> failed. Ignored when it is no longer active.</summary>
public sealed record ConnectionCheckFailed(Profile Profile, JiraError Error) : Command;
