using Geeratya.Jira.Suggestions;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>
/// The site's label list for the suggestion popup, kept for <see cref="Lifetime"/> so typing a label reads the site
/// once, not once per keystroke. The handler keeps one per connection generation
/// (<see cref="JiraServices.Generation"/>), so a profile switch replaces it. Failures and cancelled reads are never kept.
/// </summary>
public sealed class SiteLabelCache(SuggestionClient client)
{
    /// <summary>How long a read list is reused.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly object _lock = new();
    private IReadOnlyList<Suggestion>? _labels;
    private DateTimeOffset _expires;

    /// <summary>The cached list, or a fresh read when none is cached or it has expired.</summary>
    public async Task<JiraResult<IReadOnlyList<Suggestion>>> GetAsync(TimeProvider time, CancellationToken token)
    {
        lock (_lock)
        {
            if (_labels is not null && time.GetUtcNow() < _expires)
            {
                return JiraResults.Success(_labels);
            }
        }

        var result = await client.GetSiteLabelsAsync(token);

        if (result.IsSuccess)
        {
            lock (_lock)
            {
                _labels = result.Value;
                _expires = time.GetUtcNow() + Lifetime;
            }
        }

        return result;
    }
}
