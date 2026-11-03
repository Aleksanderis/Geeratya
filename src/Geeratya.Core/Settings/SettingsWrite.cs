namespace Geeratya.Core.Settings;

/// <summary>What <see cref="SettingsStore.Write"/> did.</summary>
/// <param name="Outcome">Saved, refused or found the file changed.</param>
/// <param name="Message">The reason for a refusal or a stale file; null when saved.</param>
/// <param name="Reloaded">The settings re-read from the changed file; null otherwise.</param>
public sealed record SettingsWrite(SettingsWriteOutcome Outcome, string? Message, AppSettings? Reloaded);
