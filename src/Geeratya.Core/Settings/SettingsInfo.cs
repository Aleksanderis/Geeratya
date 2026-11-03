namespace Geeratya.Core.Settings;

/// <summary>Where the files the settings screen names live; the host fills it at start (Core knows no paths).</summary>
/// <param name="SettingsFile">The path of <c>settings.json</c>; empty until known.</param>
/// <param name="KeymapFile">The path of <c>keymap.json</c>; empty until known.</param>
public sealed record SettingsInfo(string SettingsFile = "", string KeymapFile = "");
