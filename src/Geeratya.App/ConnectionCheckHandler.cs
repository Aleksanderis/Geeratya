using Geeratya.Core.State;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>
/// Runs a <see cref="CheckConnectionEffect"/>: waits for the connection of the active profile to be built, then
/// asks Jira who the account is. A reply of an older site is dropped by the <see cref="NetworkRunner"/> and, for
/// a profile that is no longer active, by the reducer. The check runs in its own scope, so closing a tab never
/// cancels it.
/// </summary>
public sealed class ConnectionCheckHandler
{
    /// <summary>The scope of connection checks; tab ids start above it.</summary>
    public const long Scope = 0;

    private readonly SiteConnections _connections;
    private readonly NetworkRunner _runner;

    public ConnectionCheckHandler(SiteConnections connections, NetworkRunner runner)
    {
        _connections = connections;
        _runner = runner;
    }

    /// <summary>
    /// Starts the check and returns the task that ends when its answer is posted or dropped. When the connection
    /// cannot be built nothing is posted here: <see cref="ConnectionFailed"/> already reports that.
    /// </summary>
    public async Task StartAsync(CheckConnectionEffect effect)
    {
        var generation = _connections.Generation;

        await _connections.WhenReadyAsync();

        if (!_connections.IsCurrent(generation))
        {
            return;
        }

        await _runner.Start(Scope, (services, token) => CheckAsync(effect, services, token));
    }

    private static async Task<Command?> CheckAsync(
        CheckConnectionEffect effect,
        JiraServices services,
        CancellationToken token)
    {
        try
        {
            var result = await services.Users.GetMyselfAsync(token);

            return result.IsSuccess
                ? new ConnectionChecked(effect.Profile, result.Value)
                : new ConnectionCheckFailed(effect.Profile, result.Error);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var error = new JiraError(
                JiraErrorKind.Unexpected,
                null,
                $"The check failed unexpectedly ({exception.GetType().Name}).");

            return new ConnectionCheckFailed(effect.Profile, error);
        }
    }
}
