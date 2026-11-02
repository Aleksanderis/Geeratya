using Geeratya.Core.Settings;
using Geeratya.Core.State;

namespace Geeratya.App;

/// <summary>Writes the settings file for <see cref="SaveSettingsEffect"/>; answers only when that fails.</summary>
public sealed class SettingsEffectHandler
{
    private readonly SettingsStore _store;

    public SettingsEffectHandler(SettingsStore store)
    {
        _store = store;
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
            _store.Save(save.Settings);

            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ProfileOperationFailed($"Could not write settings.json: {exception.Message}");
        }
    }
}
