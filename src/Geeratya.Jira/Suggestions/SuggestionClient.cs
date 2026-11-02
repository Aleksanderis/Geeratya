using System.Globalization;
using System.Text.RegularExpressions;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Suggestions;

/// <summary>
/// Read-only lookups behind pickers: text in, a short list out. Every call is cancellable (a cancelled call
/// throws <see cref="OperationCanceledException"/>) and at most <see cref="SuggestionFilter.MaxResults"/> long.
/// An empty query sends no request and returns nothing, except issues, where Jira's own recents answer.
/// Nothing is cached here (the app keeps a short-lived site label list, #292). A hidden project or issue is <see cref="JiraErrorKind.NotFound"/>. How team-managed
/// projects answer for components and versions is not yet recorded (#260); the client treats them like any list.
/// </summary>
public sealed partial class SuggestionClient(JiraTransport transport)
{
    /// <summary>Largest number of label pages read when searching the site's labels.</summary>
    public const int MaxLabelPages = 5;

    private const string ApiPath = "/rest/api/3";
    private const string ApiPrefix = "/rest/api/";
    private const string AssignableSearch = "user/assignable/search";
    private const string ViewIssueSearch = "user/viewissue/search";
    private const string LabelPath = ApiPath + "/label";
    private const string PickerPath = ApiPath + "/issue/picker";
    private const string ProjectPath = ApiPath + "/project";
    private const string ComponentsSegment = "components";
    private const string VersionsSegment = "versions";
    private const int LabelPageSize = 1000;

    /// <summary>People who can be assigned issues of a project (the create form).</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchAssignableUsersForProjectAsync(
        string projectKey,
        string? query,
        CancellationToken cancellationToken)
    {
        if (!IsProjectKey(projectKey))
        {
            return Task.FromResult(NotFound());
        }

        return SearchUsersAsync($"{AssignableSearch}?project={projectKey}", query, cancellationToken);
    }

    /// <summary>People who can be assigned one issue (the assignee editor).</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchAssignableUsersAsync(
        string issueKey,
        string? query,
        CancellationToken cancellationToken)
    {
        if (!IssueUrls.IsValidKey(issueKey))
        {
            return Task.FromResult(NotFound());
        }

        return SearchUsersAsync($"{AssignableSearch}?issueKey={issueKey}", query, cancellationToken);
    }

    /// <summary>People who can see an issue and so can be mentioned in its comments.</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchMentionableUsersAsync(
        string issueKey,
        string? query,
        CancellationToken cancellationToken)
    {
        if (!IssueUrls.IsValidKey(issueKey))
        {
            return Task.FromResult(NotFound());
        }

        return SearchUsersAsync($"{ViewIssueSearch}?issueKey={issueKey}", query, cancellationToken);
    }

    /// <summary>
    /// Labels matching <paramref name="query"/>: those already on visible issues (<paramref name="knownLabels"/>,
    /// no request) and the site's labels (<c>GET /label</c> has no query parameter, so up to
    /// <see cref="MaxLabelPages"/> pages are read and filtered here). A failure of the site call is returned only
    /// when nothing local matches.
    /// </summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchLabelsAsync(
        string? query,
        IEnumerable<string> knownLabels,
        CancellationToken cancellationToken) =>
        SearchLabelsFromAsync(query, knownLabels, GetSiteLabelsAsync, cancellationToken);

    /// <summary>
    /// Like <see cref="SearchLabelsAsync(string?, IEnumerable{string}, CancellationToken)"/>, but the site's label list comes from <paramref name="siteLabels"/> (a caller's
    /// short-lived cache around <see cref="GetSiteLabelsAsync"/>); a blank query reads nothing.
    /// </summary>
    public static async Task<JiraResult<IReadOnlyList<Suggestion>>> SearchLabelsFromAsync(
        string? query,
        IEnumerable<string> knownLabels,
        Func<CancellationToken, Task<JiraResult<IReadOnlyList<Suggestion>>>> siteLabels,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return JiraResults.Success<IReadOnlyList<Suggestion>>([]);
        }

        var local = knownLabels.Select(SuggestionFilter.FromLabel).ToList();
        var site = await siteLabels(cancellationToken);

        if (!site.IsSuccess)
        {
            return LocalOrError(local, query, site.Error);
        }

        return JiraResults.Success(SuggestionFilter.Filter([.. local, .. site.Value], query));
    }

    /// <summary>
    /// The site's labels, unfiltered (<c>GET /label</c> has no query parameter): up to <see cref="MaxLabelPages"/>
    /// pages are read in sequence. The client keeps nothing; callers cache the list for a short time.
    /// </summary>
    public async Task<JiraResult<IReadOnlyList<Suggestion>>> GetSiteLabelsAsync(CancellationToken cancellationToken)
    {
        var all = new List<Suggestion>();
        var startAt = 0;

        for (var page = 0; page < MaxLabelPages; page++)
        {
            var result = await transport.SendAsync(
                JiraRequest.Get($"{LabelPath}?startAt={Number(startAt)}&maxResults={Number(LabelPageSize)}"),
                SuggestionJsonContext.Default.LabelPage,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return JiraResults.Failure<IReadOnlyList<Suggestion>>(result.Error);
            }

            var rawCount = result.Value.Values?.Count ?? 0;
            all.AddRange(SuggestionMapper.MapLabels(result.Value).Select(SuggestionFilter.FromLabel));
            startAt += rawCount;

            if (rawCount == 0 || result.Value.IsLast != false)
            {
                break;
            }
        }

        return JiraResults.Success<IReadOnlyList<Suggestion>>(all);
    }

    /// <summary>
    /// Issues by key prefix or text (<c>GET /issue/picker</c>). <paramref name="currentJql"/> narrows the search
    /// to the view's scope when given. An empty query returns Jira's recent issues.
    /// </summary>
    public async Task<JiraResult<IReadOnlyList<Suggestion>>> SearchIssuesAsync(
        string? query,
        string? currentJql,
        CancellationToken cancellationToken)
    {
        var url = $"{PickerPath}?query={Uri.EscapeDataString(query?.Trim() ?? string.Empty)}";

        if (!string.IsNullOrWhiteSpace(currentJql))
        {
            url = $"{url}&currentJQL={Uri.EscapeDataString(currentJql)}";
        }

        var result = await transport.SendAsync(
            JiraRequest.Get(url),
            SuggestionJsonContext.Default.Picker,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return JiraResults.Failure<IReadOnlyList<Suggestion>>(result.Error);
        }

        return JiraResults.Success(Take(SuggestionMapper.MapPicker(result.Value)));
    }

    /// <summary>Components of a project whose name contains <paramref name="query"/>.</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchComponentsAsync(
        string projectKey,
        string? query,
        CancellationToken cancellationToken) =>
        SearchProjectListAsync(projectKey, ComponentsSegment, query, cancellationToken);

    /// <summary>Versions of a project whose name contains <paramref name="query"/> (archived ones marked).</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchVersionsAsync(
        string projectKey,
        string? query,
        CancellationToken cancellationToken) =>
        SearchProjectListAsync(projectKey, VersionsSegment, query, cancellationToken);

    /// <summary>
    /// A custom picker's own source (<see cref="CreateField.AutoCompleteUrl"/>). Only a URL on the connection's own
    /// scheme and host under <c>/rest/api/</c> is requested; anything else (including a site URL with a path prefix,
    /// such as the scoped-token gateway) is refused with no request. The query is appended to the URL, so the URL
    /// must end where the query text goes (<c>...?query=</c>). The reply is read in the <c>suggestions</c> shape
    /// (label, value); other shapes give an empty list.
    /// </summary>
    public async Task<JiraResult<IReadOnlyList<Suggestion>>> SearchByAutoCompleteUrlAsync(
        string autoCompleteUrl,
        string? query,
        CancellationToken cancellationToken)
    {
        var path = OwnApiPath(autoCompleteUrl);

        if (path is null)
        {
            return Refused();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return JiraResults.Success<IReadOnlyList<Suggestion>>([]);
        }

        var result = await transport.SendAsync(
            JiraRequest.Get($"{path}{Uri.EscapeDataString(query.Trim())}"),
            SuggestionJsonContext.Default.AutoComplete,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return JiraResults.Failure<IReadOnlyList<Suggestion>>(result.Error);
        }

        return JiraResults.Success(Take(SuggestionMapper.MapAutoComplete(result.Value)));
    }

    /// <summary>
    /// A select-like field's own options, filtered here: <see cref="CreateField.AllowedValues"/> come with the
    /// create or edit metadata, so no request is made.
    /// </summary>
    public static IReadOnlyList<Suggestion> SearchAllowedValues(CreateField field, string? query) =>
        SuggestionFilter.Filter(
            field.AllowedValues.Select(value => new Suggestion(value.Id, value.Label, null, false)),
            query);

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectKeyPattern();

    private async Task<JiraResult<IReadOnlyList<Suggestion>>> SearchUsersAsync(
        string pathWithScope,
        string? query,
        CancellationToken cancellationToken)
    {
        var text = query?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return JiraResults.Success<IReadOnlyList<Suggestion>>([]);
        }

        var url = $"{ApiPath}/{pathWithScope}&query={Uri.EscapeDataString(text)}"
            + $"&maxResults={Number(SuggestionFilter.MaxResults)}";
        var result = await transport.SendAsync(
            JiraRequest.Get(url),
            SuggestionJsonContext.Default.ListUser,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return JiraResults.Failure<IReadOnlyList<Suggestion>>(result.Error);
        }

        return JiraResults.Success(Take(SuggestionMapper.MapUsers(result.Value)));
    }

    private async Task<JiraResult<IReadOnlyList<Suggestion>>> SearchProjectListAsync(
        string projectKey,
        string segment,
        string? query,
        CancellationToken cancellationToken)
    {
        if (!IsProjectKey(projectKey))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return JiraResults.Success<IReadOnlyList<Suggestion>>([]);
        }

        var result = await transport.SendAsync(
            JiraRequest.Get($"{ProjectPath}/{projectKey}/{segment}"),
            SuggestionJsonContext.Default.ListNamed,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return JiraResults.Failure<IReadOnlyList<Suggestion>>(result.Error);
        }

        return JiraResults.Success(SuggestionFilter.Filter(SuggestionMapper.MapNamed(result.Value), query));
    }

    // The path and query of a URL on the connection's own host under the REST prefix; null for anything else.
    private string? OwnApiPath(string url)
    {
        var site = transport.Connection.SiteUrl;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, site.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Authority, site.Authority, StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.StartsWith(ApiPrefix, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        return uri.PathAndQuery;
    }

    private static JiraResult<IReadOnlyList<Suggestion>> LocalOrError(
        List<Suggestion> local,
        string query,
        JiraError error)
    {
        var fallback = SuggestionFilter.Filter(local, query);

        if (fallback.Count > 0)
        {
            return JiraResults.Success(fallback);
        }

        return JiraResults.Failure<IReadOnlyList<Suggestion>>(error);
    }

    private static bool IsProjectKey(string? key) =>
        key is { Length: > 0 and <= 255 } && ProjectKeyPattern().IsMatch(key);

    private static IReadOnlyList<Suggestion> Take(IReadOnlyList<Suggestion> items)
    {
        if (items.Count <= SuggestionFilter.MaxResults)
        {
            return items;
        }

        return items.Take(SuggestionFilter.MaxResults).ToList();
    }

    private static JiraResult<IReadOnlyList<Suggestion>> NotFound() =>
        JiraResults.Failure<IReadOnlyList<Suggestion>>(
            new JiraError(JiraErrorKind.NotFound, null, "Not found or no access."));

    private static JiraResult<IReadOnlyList<Suggestion>> Refused() =>
        JiraResults.Failure<IReadOnlyList<Suggestion>>(
            new JiraError(JiraErrorKind.NotFound, null, "Not a Jira address of this site."));

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
