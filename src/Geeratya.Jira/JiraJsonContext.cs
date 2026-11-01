using System.Text.Json.Serialization;
using Geeratya.Jira.Users;

namespace Geeratya.Jira;

/// <summary>Source-generated JSON metadata of the Jira layer. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(JiraUser))]
public sealed partial class JiraJsonContext : JsonSerializerContext;
