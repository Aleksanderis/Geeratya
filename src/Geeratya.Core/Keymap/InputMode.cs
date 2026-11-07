using System.Text.Json.Serialization;

namespace Geeratya.Core.Keymap;

/// <summary>The input mode decides which keys act and which are text (<c>docs/internal/ux.md</c>, "Modes").</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InputMode>))]
public enum InputMode
{
    [JsonStringEnumMemberName("normal")]
    Normal,

    [JsonStringEnumMemberName("command")]
    Command,

    [JsonStringEnumMemberName("help")]
    Help,

    [JsonStringEnumMemberName("confirm")]
    Confirm,

    [JsonStringEnumMemberName("insert")]
    Insert,
}
