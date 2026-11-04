using Geeratya.Core.State;

namespace Geeratya.App;

/// <summary>
/// A handler for effects that need no Jira connection (files, the secret store, the browser launcher). The
/// <see cref="EffectRunner"/> hands an effect to the first handler that claims it.
/// </summary>
public interface ILocalEffectHandler
{
    /// <summary>Whether this handler performs <paramref name="effect"/>.</summary>
    bool CanRun(Effect effect);

    /// <summary>The command that reports the outcome, or null when there is nothing to report.</summary>
    Task<Command?> RunAsync(Effect effect, CancellationToken cancellationToken);
}
