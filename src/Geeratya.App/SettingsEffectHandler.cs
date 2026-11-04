using Geeratya.Core.Settings;
using Geeratya.Core.State;

namespace Geeratya.App;

/// <summary>Writes one setting into the settings file for <see cref="SaveSettingsEffect"/>; answers only when that fails.</summary>
public sealed class SettingsEffectHandler : ILocalEffectHandler
{
    private readonly SettingsStore _store;

    public SettingsEffectHandler(SettingsStore store)
    {
        _store = store;
    }

    public bool CanRun(Effect effect)
    {
        return effect is SaveSettingsEffect;
    }

    public Task<Command?> RunAsync(Effect effect, CancellationToken cancellationToken)
    {
        return Task.FromResult(Run(effect));
    }

    /// <summary>A failure command, or null on success and for effects that are not settings effects.</summary>
    public Command? Run(Effect effect)
    {
        if (effect is not SaveSettingsEffect save)
        {
            return null;
        }

        try
        {
            var written = _store.Write(save.Setting, save.Settings);

            return Answer(written);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ProfileOperationFailed($"Could not write settings.json: {exception.Message}");
        }
    }

    private static Command? Answer(SettingsWrite written)
    {
        return written.Outcome switch
        {
            SettingsWriteOutcome.Saved => null,
            SettingsWriteOutcome.Stale when written.Reloaded is { } reloaded =>
                new SettingsReloaded(reloaded, written.Message ?? string.Empty),
            _ => new ProfileOperationFailed(written.Message ?? "Could not write settings.json."),
        };
    }
}
