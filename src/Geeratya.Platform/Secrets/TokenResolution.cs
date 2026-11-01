namespace Geeratya.Platform.Secrets;

/// <summary>A token and where it came from. <see cref="ToString"/> never shows the token.</summary>
public sealed record TokenResolution(string? Token, TokenSource Source)
{
    public override string ToString()
    {
        return $"TokenResolution {{ Source = {Source}, HasToken = {Token is not null} }}";
    }
}
