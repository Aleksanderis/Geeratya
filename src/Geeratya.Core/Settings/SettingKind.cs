namespace Geeratya.Core.Settings;

/// <summary>How a setting is typed in <c>:set</c> and stored in <c>settings.json</c>.</summary>
public enum SettingKind
{
    /// <summary><c>on</c> or <c>off</c>; a JSON boolean in the file.</summary>
    Toggle,

    /// <summary>One word of a fixed list (<c>full</c>, <c>minimal</c>, <c>off</c>); a JSON string in the file, and a boolean is read as <c>on</c> or <c>off</c>.</summary>
    Choice,

    /// <summary>A name (a theme); a JSON string in the file.</summary>
    Text,
}
