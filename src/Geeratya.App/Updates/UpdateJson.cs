using System.Text.Json.Serialization;

namespace Geeratya.App.Updates;

/// <summary>The parts of GitHub's release JSON that the update reads.</summary>
public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// The file <c>update-check.json</c> in the state folder: when GitHub was asked last, and what it said.
/// </summary>
public sealed class UpdateCheckRecord
{
    [JsonPropertyName("checkedAt")]
    public DateTimeOffset CheckedAt { get; set; }

    [JsonPropertyName("latest")]
    public string? Latest { get; set; }
}

[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(UpdateCheckRecord))]
internal sealed partial class UpdateJsonContext : JsonSerializerContext;
