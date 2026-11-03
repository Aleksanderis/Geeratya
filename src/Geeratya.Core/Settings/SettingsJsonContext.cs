using System.Text.Json.Serialization;

namespace Geeratya.Core.Settings;

[JsonSerializable(typeof(string))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
