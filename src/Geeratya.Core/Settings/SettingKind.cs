namespace Geeratya.Core.Settings;

/// <summary>How a setting is typed in <c>:set</c> and stored in <c>settings.json</c>.</summary>
public enum SettingKind
{
    /// <summary><c>on</c> or <c>off</c>; a JSON boolean in the file.</summary>
    Toggle,

    /// <summary>A name (a theme); a JSON string in the file.</summary>
    Text,
}
