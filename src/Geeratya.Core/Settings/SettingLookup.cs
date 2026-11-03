using System.Text.Json;

namespace Geeratya.Core.Settings;

/// <summary>A setting's value in the file (null when absent) and the key it was found at.</summary>
public sealed record SettingLookup(JsonElement? Value, string Key);
