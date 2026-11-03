using Geeratya.Core.Settings;

namespace Geeratya.Core.State;

/// <summary>
/// Write one setting into the settings file (only its value is replaced); a failure is reported and the change stays
/// on for the session.
/// </summary>
public sealed record SaveSettingsEffect(SettingDefinition Setting, AppSettings Settings) : Effect;
