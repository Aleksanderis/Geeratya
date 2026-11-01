using Geeratya.Core.Browser;

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
            return Say(state, "Cannot open the link: no active site");
        }

        var check = BrowserUrlCheck.Check(request.Url, request.IsRemoteLink ? null : host);

        if (check.Url is null)
        {
            return Say(state, $"Cannot open the link: {check.Reason}");
        }

        return new ReduceResult(state with { Message = "Opening in the browser..." }, [new OpenUrlEffect(check.Url)]);
    }

    public static ReduceResult Finished(AppState state, UrlOpenFinished finished)
    {
        var message = finished.Error is null ? "Opened in the browser" : $"Cannot open the link: {finished.Error}";

        return Say(state, message);
    }

    private static string RequiredHost(AppState state, OpenUrlRequested request)
    {
        if (request.IsRemoteLink || state.Profiles.Active is null)
        {
            return string.Empty;
        }

        return Uri.TryCreate(state.Profiles.Active.SiteUrl, UriKind.Absolute, out var site) ? site.Host : string.Empty;
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }
}
