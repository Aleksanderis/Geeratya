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
/// Nothing is cached here (the app keeps a short-lived site label list, #292).
/// A hidden project or issue is <see cref="JiraErrorKind.NotFound"/>. How team-managed
/// projects answer for components and versions is not yet recorded (#260); the client treats them like any list.
/// </summary>
public sealed partial class SuggestionClient(JiraTransport transport)
{
    /// <summary>Largest number of label pages read when searching the site's labels.</summary>
    public const int MaxLabelPages = 5;

    private const string ApiPath = "/rest/api/3";
    private const string AssignableSearch = "user/assignable/search";
    private const string ViewIssueSearch = "user/viewissue/search";
    private const string LabelPath = ApiPath + "/label";
    private const string PickerPath = ApiPath + "/issue/picker";
    /// <summary>How long the issue-type lookup of an issue list may take before the rows come without a type.</summary>
    public static readonly TimeSpan TypeSearchLimit = TimeSpan.FromSeconds(2);

    private const string SearchPath =ApiPath + "/search/jql";
    private const string ProjectPath = ApiPath + "/project";
    private const string ComponentsSegment = "components";
    private const string VersionsSegment = "versions";
    private const int LabelPageSize = 1000;

    /// <summary>People who can see issues of a project (a person field of the create form other than the assignee).</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> SearchProjectUsersAsync(
        string projectKey,
        string? query,
        CancellationToken cancellationToken)
    {
        if (!IsProjectKey(projectKey))
        {
            return Task.FromResult(NotFound());
        }

        return SearchUsersAsync($"{ViewIssueSearch}?projectKey={projectKey}", query, cancellationToken);
    }

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
    /// Like <see cref="SearchLabelsAsync(string?, IEnumerable{string}, CancellationToken)"/>, but the site's label
    /// list comes from <paramref name="siteLabels"/> (a caller's short-lived cache around
    /// <see cref="GetSiteLabelsAsync"/>); a blank query reads nothing.
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

        var rows = Take(SuggestionMapper.MapPicker(result.Value));

        return JiraResults.Success(await WithTypesAsync(rows, cancellationToken));
    }

    // The picker names no issue type, so one more read (a key-list search asking for issuetype only) adds it. A
    // failed read leaves the rows as they are: the list still works, it just shows no type.
    private async Task<IReadOnlyList<Suggestion>> WithTypesAsync(
        IReadOnlyList<Suggestion> rows,
        CancellationToken cancellationToken)
    {
        var keys = rows.Select(row => row.Id).Where(key => IssueKeyPattern().IsMatch(key)).ToList();

        if (keys.Count == 0)
        {
            return rows;
        }

        var jql = $"key in ({string.Join(",", keys)})";
        var url = $"{SearchPath}?jql={Uri.EscapeDataString(jql)}&fields=issuetype&maxResults={keys.Count}";

        // A slow type search must not hold the list back: past the limit the rows come without a type.
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        bounded.CancelAfter(TypeSearchLimit);

        try
        {
            var result = await transport.SendAsync(
                JiraRequest.Get(url),
                SuggestionJsonContext.Default.TypeSearch,
                bounded.Token);

            return result.IsSuccess ? SuggestionMapper.WithTypes(rows, result.Value) : rows;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return rows;
        }
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
    /// A select-like field's own options, filtered here: <see cref="CreateField.AllowedValues"/> come with the
    /// create or edit metadata, so no request is made.
    /// </summary>
    public static IReadOnlyList<Suggestion> SearchAllowedValues(CreateField field, string? query) =>
        SuggestionFilter.Filter(
            field.AllowedValues.Select(value => new Suggestion(value.Id, value.Label, null, false)),
            query);

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*-[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex IssueKeyPattern();

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

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
