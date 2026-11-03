using System.Text.Json.Serialization;

namespace Geeratya.Core.Startup;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(RememberedTabs))]
internal sealed partial class StartupJsonContext : JsonSerializerContext;
