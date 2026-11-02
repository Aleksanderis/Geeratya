using Geeratya.Core.State;
using Geeratya.Platform.Browser;

namespace Geeratya.App;

/// <summary>Runs <see cref="OpenUrlEffect"/> through the platform launcher and reports the outcome.</summary>
public sealed class BrowserEffectHandler
{
    private readonly IBrowserLauncher _launcher;

    public BrowserEffectHandler(IBrowserLauncher launcher)
    {
        _launcher = launcher;
    }

    /// <summary>The command that reports the outcome, or null when the effect is not an open-URL effect.</summary>
    public async Task<Command?> RunAsync(Effect effect, CancellationToken cancellationToken)
    {
        if (effect is not OpenUrlEffect open)
        {
            return null;
        }

        try
        {
            var result = await _launcher.OpenAsync(open.Url, cancellationToken);

            return new UrlOpenFinished(result.Opened ? null : result.Message, open.Label);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            return new UrlOpenFinished($"the browser could not be started ({exception.GetType().Name})", open.Label);
        }
    }
}
