using System.Text.Json.Serialization;

namespace Geeratya.Core.Profiles;

/// <summary>
/// A saved Jira site plus account. The API token is not part of it: it lives in the secret store (or
/// <c>GEERATYA_TOKEN</c>) under <see cref="CredentialKey"/>.
/// </summary>
public sealed record Profile(string Name, string SiteUrl, string Email)
{
    /// <summary>The reference to this profile's token: stable for the same site and account.</summary>
    [JsonIgnore]
    public string CredentialKey => $"{SiteUrl.TrimEnd('/').ToLowerInvariant()}|{Email.ToLowerInvariant()}";
}
