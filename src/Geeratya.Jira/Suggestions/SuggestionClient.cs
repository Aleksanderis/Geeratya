using System.Globalization;
using System.Text.RegularExpressions;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Suggestions;

/// <summary>
/// Read-only lookups behind pickers: text in, a short ranked list out. Every call is cancellable (a cancelled call
/// throws <see cref="OperationCanceledException"/>) and at most <see cref="SuggestionFilter.MaxResults"/> long.
/// An empty query sends no request and returns nothing, except issues, where Jira's own recents answer.
/// Nothing is cached here. A hidden project or issue is <see cref="JiraErrorKind.NotFound"/>. Team-managed projects
/// have no components and their own version model: those calls return an empty list, not an error.
/// </summary>
public sealed partial class SuggestionClient(JiraTransport transport)
{
    /// <summary>Largest number of label pages read when searching the site's labels.</summary>
    public const int MaxLabelPages = 5;

    private const string ApiPath = "/rest/api/3";
    private const string ApiPrefix = "/rest/api/";
    private const int LabelPageSize = 1000;

    /// <summary>People who can be assigned issues of a project (the create form).</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchAssignableUsersForProjectAsync(
        string projectKey,
        string? query,
        CancellationToken cancellationToken) =>
        IsProjectKey(projectKey)
            ? SearchUsersAsync($"user/assignable/search?project={projectKey}", query, cancellationToken)
            : Task.FromResult(NotFound());

    /// <summary>People who can be assigned one issue (the assignee editor).</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchAssignableUsersAsync(
        string issueKey,
        string? query,
        CancellationToken cancellationToken) =>
        IssueUrls.IsValidKey(issueKey)
            ? SearchUsersAsync($"user/assignable/search?issueKey={issueKey}", query, cancellationToken)
            : Task.FromResult(NotFound());

    /// <summary>People who can see an issue and so can be mentioned in its comments.</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchMentionableUsersAsync(
        string issueKey,
        string? query,
        CancellationToken cancellationToken) =>
        IssueUrls.IsValidKey(issueKey)
            ? SearchUsersAsync($"user/viewissue/search?issueKey={issueKey}", query, cancellationToken)
            : Task.FromResult(NotFound());

    /// <summary>
    /// Labels matching <paramref name="query"/>: those already on visible issues (<paramref name="knownLabels"/>,
    /// no request) and the site's labels (<c>GET /label</c> has no query parameter, so up to
    /// <see cref="MaxLabelPages"/> pages are read and filtered here). A failure of the site call is returned only
    /// when nothing local matches.
    /// </summary>
    public async Task<JiraResult<IReadOnlyList<Suggestion>>> SearchLabelsAsync(
        string? query,
        IEnumerable<string> knownLabels,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return JiraResults.Success<IReadOnlyList<Suggestion>>([]);
        }

        var local = knownLabels.Select(SuggestionFilter.FromLabel).ToList();
        var candidates = new List<Suggestion>(local);
        var startAt = 0;

        for (var page = 0; page < MaxLabelPages; page++)
        {
            var result = await transport.SendAsync(
                JiraRequest.Get($"{ApiPath}/label?startAt={Number(startAt)}&maxResults={Number(LabelPageSize)}"),
                SuggestionJsonContext.Default.LabelPage,
                cancellationToken);

            if (!result.IsSuccess)
            {
                var fallback = SuggestionFilter.Filter(local, query);

                return fallback.Count > 0
                    ? JiraResults.Success(fallback)
                    : JiraResults.Failure<IReadOnlyList<Suggestion>>(result.Error);
            }

            var labels = SuggestionMapper.MapLabels(result.Value);
            candidates.AddRange(labels.Select(SuggestionFilter.FromLabel));
            startAt += labels.Count;

            if (labels.Count == 0 || result.Value.IsLast != false)
            {
                break;
            }
        }

        return JiraResults.Success(SuggestionFilter.Filter(candidates, query));
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
        var url = $"{ApiPath}/issue/picker?query={Uri.EscapeDataString(query?.Trim() ?? string.Empty)}";

        if (!string.IsNullOrWhiteSpace(currentJql))
        {
            url = $"{url}&currentJQL={Uri.EscapeDataString(currentJql)}";
        }

        var result = await transport.SendAsync(
            JiraRequest.Get(url),
            SuggestionJsonContext.Default.Picker,
            cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(Take(SuggestionMapper.MapPicker(result.Value)))
            : JiraResults.Failure<IReadOnlyList<Suggestion>>(result.Error);
    }

    /// <summary>Components of a project whose name contains <paramref name="query"/>.</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchComponentsAsync(
        string projectKey,
        string? query,
        CancellationToken cancellationToken) =>
        SearchProjectListAsync(projectKey, "components", query, cancellationToken);

    /// <summary>Versions of a project whose name contains <paramref name="query"/> (archived ones are marked).</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchVersionsAsync(
        string projectKey,
        string? query,
        CancellationToken cancellationToken) =>
        SearchProjectListAsync(projectKey, "versions", query, cancellationToken);

    /// <summary>
    /// A custom picker's own source (<see cref="CreateField.AutoCompleteUrl"/>). Only a URL on the connection's own
    /// host and under <c>/rest/api/</c> is requested; anything else is refused with no request. The query is
    /// appended to the URL, so it must end where the query text goes (<c>...?query=</c>). The reply is read in
    /// the <c>suggestions</c> shape (label, value); other shapes give an empty list.
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

        return result.IsSuccess
            ? JiraResults.Success(Take(SuggestionMapper.MapAutoComplete(result.Value)))
            : JiraResults.Failure<IReadOnlyList<Suggestion>>(result.Error);
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

        return result.IsSuccess
            ? JiraResults.Success(Take(SuggestionMapper.MapUsers(result.Value)))
            : JiraResults.Failure<IReadOnlyList<Suggestion>>(result.Error);
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
            JiraRequest.Get($"{ApiPath}/project/{projectKey}/{segment}"),
            SuggestionJsonContext.Default.ListNamed,
            cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(SuggestionFilter.Filter(SuggestionMapper.MapNamed(result.Value), query))
            : JiraResults.Failure<IReadOnlyList<Suggestion>>(result.Error);
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

    private static bool IsProjectKey(string? key) => key is { Length: > 0 and <= 255 } && ProjectKeyPattern().IsMatch(key);

    private static IReadOnlyList<Suggestion> Take(IReadOnlyList<Suggestion> items) =>
        items.Count <= SuggestionFilter.MaxResults ? items : items.Take(SuggestionFilter.MaxResults).ToList();

    private static JiraResult<IReadOnlyList<Suggestion>> NotFound() =>
        JiraResults.Failure<IReadOnlyList<Suggestion>>(
            new JiraError(JiraErrorKind.NotFound, null, "Not found or no access."));

    private static JiraResult<IReadOnlyList<Suggestion>> Refused() =>
        JiraResults.Failure<IReadOnlyList<Suggestion>>(
            new JiraError(JiraErrorKind.NotFound, null, "Not a Jira address of this site."));

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
