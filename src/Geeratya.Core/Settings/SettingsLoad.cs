namespace Geeratya.Core.Settings;

/// <summary>The settings read from the file plus a warning per problem (invalid value, file that does not parse).</summary>
public sealed record SettingsLoad(AppSettings Settings, IReadOnlyList<string> Warnings);
