namespace Geeratya.Core.Settings;

public enum SettingsWriteOutcome
{
    Saved,

    /// <summary>The file does not parse or is not an object; it was not written.</summary>
    Refused,

    /// <summary>The file changed since it was read; it was re-read and nothing was written.</summary>
    Stale,
}
