using Geeratya.Jira.Suggestions;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>
/// The site's label list for the suggestion popup, kept for <see cref="Lifetime"/> so typing a label reads the site
/// once, not once per keystroke. The handler keeps one per connection generation
/// (<see cref="JiraServices.Generation"/>), so a profile switch replaces it. A read in flight is shared: a caller
/// that is cancelled (the next keystroke) stops only its own wait, the read goes on for the next caller. Failures
/// are not kept.
/// </summary>
public sealed class SiteLabelCache(SuggestionClient client, TimeProvider time)
{
    /// <summary>How long a read list is reused.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly object _lock = new();
    private Task<JiraResult<IReadOnlyList<Suggestion>>>? _read;
    private DateTimeOffset _expires;

    /// <summary>The cached list, or the read in flight, or a fresh read when none is cached or it has expired.</summary>
    public Task<JiraResult<IReadOnlyList<Suggestion>>> GetAsync(CancellationToken token)
    {
        Task<JiraResult<IReadOnlyList<Suggestion>>> read;

        lock (_lock)
        {
            if (_read is null || (_read.IsCompleted && time.GetUtcNow() >= _expires))
            {
                _read = ReadAsync();
            }

            read = _read;
        }

        return read.WaitAsync(token);
    }

    // Runs under the transport's own timeout, not a caller's token, so it survives the caller that started it.
    private async Task<JiraResult<IReadOnlyList<Suggestion>>> ReadAsync()
    {
        var result = await client.GetSiteLabelsAsync(CancellationToken.None);

        lock (_lock)
        {
            if (result.IsSuccess)
            {
                _expires = time.GetUtcNow() + Lifetime;
            }
            else
            {
                _expires = DateTimeOffset.MinValue;
            }
        }

        return result;
    }
}
