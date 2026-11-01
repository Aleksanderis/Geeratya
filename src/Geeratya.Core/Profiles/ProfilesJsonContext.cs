using System.Text.Json.Serialization;

namespace Geeratya.Core.Profiles;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(ProfileCatalog))]
internal sealed partial class ProfilesJsonContext : JsonSerializerContext;
