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

    /// <summary>The secret store could not be read (locked or missing), so the source is not known.</summary>
    Unavailable,
}
