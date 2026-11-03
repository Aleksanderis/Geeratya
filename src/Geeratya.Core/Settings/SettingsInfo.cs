namespace Geeratya.Core.Settings;

/// <summary>Where the files the settings screen names live; the host fills it at start (Core knows no paths).</summary>
/// <param name="SettingsFile">The path of <c>settings.json</c>; empty until known.</param>
/// <param name="KeymapFile">The path of <c>keymap.json</c>; empty until known.</param>
/// <param name="Tier">The colours the terminal shows; the theme row notes it when it is not full colour.</param>
public sealed record SettingsInfo(
    string SettingsFile = "",
    string KeymapFile = "",
    ColourTier Tier = ColourTier.TrueColour);
