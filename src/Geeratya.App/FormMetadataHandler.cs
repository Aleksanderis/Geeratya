using Geeratya.Core.Forms;
using Geeratya.Core.State;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>
/// Turns <see cref="LoadFormMetadataEffect"/> and <see cref="SearchFieldOptionsEffect"/> into the command that
/// reports them. Both are reads that run under the scope of the tab, so closing the tab cancels them; the request id
/// makes the reducer apply only the current reply.
/// </summary>
public sealed class FormMetadataHandler
{
    private const string NoConnectionMessage = "Not connected to Jira: check the profile and its API token (:profile).";
    private const string UnsupportedMessage = "This form cannot be opened yet.";

    private static readonly TimeSpan _defaultDebounce = TimeSpan.FromMilliseconds(250);

    private readonly NetworkRunner _runner;
    private readonly TimeSpan _debounce;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Dictionary<long, CancellationTokenSource> _searches = [];
    private readonly Lock _lock = new();

    /// <param name="runner">Runs the reads.</param>
    /// <param name="debounce">How long a search waits for more typing; tests pass zero.</param>
    /// <param name="delay">The wait itself; tests inject one they control.</param>
    public FormMetadataHandler(
        NetworkRunner runner,
        TimeSpan? debounce = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _runner = runner;
        _debounce = debounce ?? _defaultDebounce;
        _delay = delay ?? Task.Delay;
    }

    /// <summary>
    /// Starts a search of the options for the typed text. A search of the same tab that is still waiting or running
    /// is cancelled first, and the wait lets typing settle before a request is made.
    /// </summary>
    public Task StartAsync(SearchFieldOptionsEffect effect)
    {
        var newer = new CancellationTokenSource();

        lock (_lock)
        {
            if (_searches.Remove(effect.Tab.Value, out var older))
            {
                older.Cancel();
            }

            _searches[effect.Tab.Value] = newer;
        }

        return _runner.Start(
            effect.Tab.Value,
            (services, token) => SearchThenForgetAsync(services, effect, newer, token),
            () => new FieldOptionsFailed(effect.Tab, effect.RequestId, NoConnectionMessage));
    }

    /// <summary>Starts the load and returns the task that ends when its answer is posted or dropped.</summary>
    public Task StartAsync(LoadFormMetadataEffect effect)
    {
        return _runner.Start(
            effect.Tab.Value,
            (services, token) => LoadAsync(services, effect, token),
            () => new FormMetadataFailed(effect.Tab, effect.RequestId, NoConnectionMessage));
    }

    private static async Task<Command?> LoadAsync(
        JiraServices services,
        LoadFormMetadataEffect effect,
        CancellationToken token)
    {
        if (effect.Target is not EditFormTarget edit)
        {
            return new FormMetadataFailed(effect.Tab, effect.RequestId, UnsupportedMessage);
        }

        var result = await services.Edits.GetEditFieldsAsync(edit.Key, token);

        if (!result.IsSuccess)
        {
            return new FormMetadataFailed(effect.Tab, effect.RequestId, MessageOf(result.Error, edit.Key));
        }

        return new FormMetadataLoaded(effect.Tab, effect.RequestId, FormFieldMapper.FromEdit(result.Value, edit));
    }

    // The entry of the tab is removed when its own search ends and no newer one replaced it.
    private async Task<Command?> SearchThenForgetAsync(
        JiraServices services,
        SearchFieldOptionsEffect effect,
        CancellationTokenSource source,
        CancellationToken token)
    {
        try
        {
            return await SearchAsync(services, effect, source.Token, token);
        }
        finally
        {
            lock (_lock)
            {
                if (_searches.TryGetValue(effect.Tab.Value, out var current) && current == source)
                {
                    _searches.Remove(effect.Tab.Value);
                    source.Dispose();
                }
            }
        }
    }

    private async Task<Command?> SearchAsync(
        JiraServices services,
        SearchFieldOptionsEffect effect,
        CancellationToken newer,
        CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, newer);

        try
        {
            await _delay(_debounce, linked.Token);

            var result = await services.Suggestions.SearchAssignableUsersAsync(effect.Key, effect.Query, linked.Token);

            if (!result.IsSuccess)
            {
                var message = $"Could not search people: {result.Error.Message}";

                return new FieldOptionsFailed(effect.Tab, effect.RequestId, message);
            }

            var options = result.Value
                .Where(user => !user.IsInactive)
                .Select(user => new FieldOption(user.Id, user.Label))
                .ToArray();

            return new FieldOptionsLoaded(effect.Tab, effect.RequestId, options);
        }
        catch (OperationCanceledException) when (newer.IsCancellationRequested && !token.IsCancellationRequested)
        {
            // A newer search replaced this one; its answer is the one that counts.
            return null;
        }
    }

    private static string MessageOf(JiraError error, string key)
    {
        return error.Kind == JiraErrorKind.NotFound
            ? $"{key} not found or no access"
            : $"Could not load the fields of {key}: {error.Message}";
    }
}
