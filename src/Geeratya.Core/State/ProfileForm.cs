using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// The add and edit profile form in Insert mode: every field is shown at once and <see cref="Field"/> is the focused
/// one. The token is a <see cref="SecretText"/>, so printing the form never shows it. When editing, an empty token
/// means the stored one is kept.
/// </summary>
public sealed record ProfileForm(
    ProfileFormField Field,
    string Name,
    string SiteUrl,
    string Email,
    SecretText Token,
    string? Error)
{
    public static ProfileForm Start { get; } =
        new(ProfileFormField.Name, string.Empty, string.Empty, string.Empty, SecretText.Empty, null);

    /// <summary>The name of the profile being edited, or null when a new one is added.</summary>
    public string? EditingName { get; init; }

    /// <summary>The outcome line of the last connection test; cleared by any edit.</summary>
    public string? TestResult { get; init; }

    /// <summary>The last connection test failed.</summary>
    public bool TestFailed { get; init; }

    /// <summary>A connection test is running.</summary>
    public bool Testing { get; init; }

    public bool IsEdit => EditingName is not null;

    /// <summary>The form of an existing profile, focused on the site (the token stays out of it).</summary>
    public static ProfileForm For(Profile profile)
    {
        return new ProfileForm(
            ProfileFormField.SiteUrl,
            profile.Name,
            profile.SiteUrl,
            profile.Email,
            SecretText.Empty,
            null)
        {
            EditingName = profile.Name,
        };
    }
}
