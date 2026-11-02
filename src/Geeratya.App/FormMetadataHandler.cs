using Geeratya.Core.Forms;
using Geeratya.Core.State;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>
/// Turns <see cref="LoadFormMetadataEffect"/> into the command that reports it. The fetch is a read and runs under the
/// scope of the tab, so closing the tab cancels it; the request id makes the reducer apply only the current reply.
/// </summary>
public sealed class FormMetadataHandler
{
    private const string NoConnectionMessage = "Not connected to Jira: check the profile and its API token (:profile).";
    private const string NeedsBrowserMessage =
        "This transition asks for a field Geeratya cannot edit yet; do it in the browser (<space>o).";
    private const string UnsupportedMessage = "This form cannot be opened yet.";

    private readonly NetworkRunner _runner;
    private readonly TimeProvider _clock;

    public FormMetadataHandler(NetworkRunner runner, TimeProvider? clock = null)
    {
        _runner = runner;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Starts the load and returns the task that ends when its answer is posted or dropped.</summary>
    public Task StartAsync(LoadFormMetadataEffect effect)
    {
        return _runner.Start(
            effect.Tab.Value,
            (services, token) => LoadAsync(services, effect, _clock.GetUtcNow(), token),
            () => new FormMetadataFailed(effect.Tab, effect.RequestId, NoConnectionMessage));
    }

    private static async Task<Command?> LoadAsync(
        JiraServices services,
        LoadFormMetadataEffect effect,
        DateTimeOffset at,
        CancellationToken token)
    {
        if (effect.Target is TransitionPickTarget pick)
        {
            return await LoadPickAsync(services, effect, pick, at, token);
        }

        if (effect.Target is TransitionFormTarget transition)
        {
            return await LoadTransitionAsync(services, effect, transition, token);
        }

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

    private static async Task<Command?> LoadPickAsync(
        JiraServices services,
        LoadFormMetadataEffect effect,
        TransitionPickTarget pick,
        DateTimeOffset at,
        CancellationToken token)
    {
        var result = await services.Transitions.GetTransitionsAsync(pick.Key, token);

        if (!result.IsSuccess)
        {
            return new FormMetadataFailed(effect.Tab, effect.RequestId, MessageOf(result.Error, pick.Key));
        }

        var loaded = pick.With(result.Value);

        return new FormMetadataLoaded(effect.Tab, effect.RequestId, loaded.Fields(), loaded, at);
    }

    private static async Task<Command?> LoadTransitionAsync(
        JiraServices services,
        LoadFormMetadataEffect effect,
        TransitionFormTarget target,
        CancellationToken token)
    {
        var result = await services.Transitions.GetTransitionsAsync(target.Key, token);

        if (!result.IsSuccess)
        {
            return new FormMetadataFailed(effect.Tab, effect.RequestId, MessageOf(result.Error, target.Key));
        }

        var transition = result.Value.FirstOrDefault(candidate => candidate.Id == target.Choice.Id);

        if (transition is null)
        {
            return new FormMetadataFailed(effect.Tab, effect.RequestId, target.EmptyText);
        }

        // A field Jira fills in when it is left out is not required of the user.
        var metas = transition.Fields.Select(meta => meta with { Required = meta.Required && !meta.HasDefaultValue });

        var fields = FormFieldMapper.FromTransition([.. metas]);

        if (!fields.Any(field => field.IsEditable))
        {
            return new FormMetadataFailed(effect.Tab, effect.RequestId, NeedsBrowserMessage);
        }

        return new FormMetadataLoaded(effect.Tab, effect.RequestId, fields);
    }

    private static string MessageOf(JiraError error, string key)
    {
        return error.Kind == JiraErrorKind.NotFound
            ? $"{key} not found or no access"
            : $"Could not load the fields of {key}: {error.Message}";
    }
}
