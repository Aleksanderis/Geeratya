using System.Text.Json;

namespace Geeratya.Core.Settings;

/// <summary>JSON spelling of values written into <c>settings.json</c>.</summary>
public static class SettingsJson
{
    /// <summary>A JSON string literal for the text (quotes and control characters escaped).</summary>
    public static string Quote(string text)
    {
        return JsonSerializer.Serialize(text, SettingsJsonContext.Default.String);
    }
}
