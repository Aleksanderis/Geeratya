using Geeratya.Jira.Transport;

namespace Geeratya.Core.Views;

/// <summary>
/// The one table from a Jira error to the text a failed tab shows. Pure, so the reducer builds it itself.
/// </summary>
public static class LoadErrorText
{
    public static string For(JiraError error)
    {
        return error.Kind switch
        {
            JiraErrorKind.Unauthorized => "Jira rejected the credentials. Check the token with :profile.",
            JiraErrorKind.Forbidden or JiraErrorKind.NotFound => "Not found, or you have no access to it.",
            JiraErrorKind.PageExpired => "The page expired. Refresh to start again.",
            JiraErrorKind.RateLimited => "Jira is rate limiting requests. Try again shortly.",
            JiraErrorKind.Timeout => "Jira did not answer in time.",
            JiraErrorKind.Network or JiraErrorKind.HostRefused => "Cannot reach Jira.",
            _ => error.Message,
        };
    }
}
