namespace Geeratya.Core.Updates;

/// <summary>A newer release that exists, with what the user does about it on this install.</summary>
/// <param name="Version">The newer version, without a leading <c>v</c>.</param>
/// <param name="Instructions">What to do, as a sentence fragment: "run `geeratya update`" or a package command.</param>
public sealed record UpdateNotice(string Version, string Instructions)
{
    /// <summary>The notice posted once when the check finds the release.</summary>
    public string Sentence => $"Geeratya {Version} is available: {Instructions}";

    /// <summary>The short form that stays in the status bar.</summary>
    public string Brief => $"update {Version} available";
}
