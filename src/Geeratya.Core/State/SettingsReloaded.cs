using Geeratya.Core.Settings;

namespace Geeratya.Core.State;

/// <summary>The settings file changed on disk before a write: the new values apply and the change is not saved.</summary>
public sealed record SettingsReloaded(AppSettings Settings, string Message) : Command;
