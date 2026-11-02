using System.Text.Json.Serialization;

namespace Geeratya.Jira.Issues;

/// <summary>Source-generated JSON metadata of the issue client. Unknown properties are ignored.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IssueWire.Issue))]
internal sealed partial class IssueJsonContext : JsonSerializerContext;
