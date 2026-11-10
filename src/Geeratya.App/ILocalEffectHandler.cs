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

    /// <summary>
    /// The command that ends the wait of <paramref name="effect"/> when <see cref="RunAsync"/> threw, or null when
    /// nothing waits on it (the default). Without it a throwing read leaves its caller waiting for ever (#924).
    /// </summary>
    Command? Faulted(Effect effect, Exception exception)
    {
        return null;
    }
}
