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
    private const string UnsupportedMessage = "This form cannot be opened yet.";

    private readonly NetworkRunner _runner;

    public FormMetadataHandler(NetworkRunner runner)
    {
        _runner = runner;
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

    private static string MessageOf(JiraError error, string key)
    {
        return error.Kind == JiraErrorKind.NotFound
            ? $"{key} not found or no access"
            : $"Could not load the fields of {key}: {error.Message}";
    }
}
