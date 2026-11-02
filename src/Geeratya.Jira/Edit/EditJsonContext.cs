using System.Text.Json.Serialization;
using Geeratya.Jira.Users;

namespace Geeratya.Jira.Edit;

/// <summary>Source-generated JSON metadata of the edit client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EditWire.Meta))]
[JsonSerializable(typeof(List<JiraUser>))]
internal sealed partial class EditJsonContext : JsonSerializerContext;
