using Geeratya.Core.Browser;
using Geeratya.Jira;
using Geeratya.Jira.Issues;
using Geeratya.Core.Views;

namespace Geeratya.Core.State;

/// <summary>Reduces the open-in-browser commands; refusals become a status message, never an effect.</summary>
public static class BrowserReducer
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Request(AppState state, OpenUrlRequested request)
    {
        var host = RequiredHost(state, request);

        if (host.Length == 0 && !request.IsRemoteLink)
        {
            return Warn(state, "Cannot open the link: no active site");
        }

        var check = BrowserUrlCheck.Check(request.Url, request.IsRemoteLink ? null : host);

        if (check.Url is null)
        {
            return Warn(state, $"Cannot open the link: {check.Reason}");
        }

        var opening = state with { Message = "Opening in the browser..." };

        return new ReduceResult(opening, [new OpenUrlEffect(check.Url, request.Label)]);
    }

    /// <summary>
    /// Opens the issue page of <paramref name="key"/> on the active site (<c>issue.open-in-browser</c> and
    /// <c>:browse KEY</c>). A null key means "the selected row of the active tab"; with no row there is only a hint.
    /// Views other than the list (columns, detail) reuse this by passing their own selected key.
    /// </summary>
    public static ReduceResult OpenIssue(AppState state, string? key)
    {
        key ??= SelectedKey(state);

        if (key is null)
        {
            return Warn(state, "No issue selected: select a row or use :browse KEY");
        }

        if (state.Profiles.Active is not { } profile || !Uri.TryCreate(profile.SiteUrl, UriKind.Absolute, out var site))
        {
            return Warn(state, "Cannot open the link: no active site");
        }

        var url = IssueUrls.Browse(new JiraConnection(site, profile.Email), key);

        if (url is null)
        {
            return Warn(state, "Cannot open the link: not an issue key");
        }

        return Request(state, new OpenUrlRequested(url.AbsoluteUri, false, key));
    }

    internal static string? SelectedKey(AppState state)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        if (tab is null)
        {
            return null;
        }

        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return ColumnsNavigation.SelectedIssue(tab)?.Key;
        }

        if (tab.Selected < 0 || tab.Selected >= tab.Rows.Count)
        {
            return null;
        }

        return tab.Rows[tab.Selected].Key;
    }

    public static ReduceResult Finished(AppState state, UrlOpenFinished finished)
    {
        if (finished.Error is not null)
        {
            return Warn(state, $"Cannot open the link: {finished.Error}");
        }

        return Say(state, finished.Label is null ? "Opened in the browser" : $"Opened {finished.Label} in the browser");
    }

    private static string RequiredHost(AppState state, OpenUrlRequested request)
    {
        if (request.IsRemoteLink || state.Profiles.Active is null)
        {
            return string.Empty;
        }

        return Uri.TryCreate(state.Profiles.Active.SiteUrl, UriKind.Absolute, out var site) ? site.Host : string.Empty;
    }

    private static ReduceResult Warn(AppState state, string message)
    {
        return new ReduceResult(StatusMessages.Warn(state, message), _noEffects);
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }
}
