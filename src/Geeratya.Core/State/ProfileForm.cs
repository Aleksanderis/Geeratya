using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// The add-profile form in Insert mode: one field at a time. The token is a <see cref="SecretText"/>, so printing
/// the form never shows it.
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
}
