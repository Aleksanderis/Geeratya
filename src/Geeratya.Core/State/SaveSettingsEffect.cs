using Geeratya.Core.Settings;

namespace Geeratya.Core.State;

/// <summary>Write the settings file; a failure is reported, the switch stays on for the session.</summary>
public sealed record SaveSettingsEffect(AppSettings Settings) : Effect;
