namespace Geeratya.Core.Profiles;

/// <summary>Where a profile's API token comes from, as the profile manager shows it (never the token itself).</summary>
public enum TokenSourceKind
{
    /// <summary>No token is stored and none is supplied.</summary>
    None,

    /// <summary>The OS secret store holds it.</summary>
    SecretStore,

    /// <summary><c>GEERATYA_TOKEN</c> supplies it (only for a single profile without a stored token).</summary>
    EnvironmentVariable,
}
