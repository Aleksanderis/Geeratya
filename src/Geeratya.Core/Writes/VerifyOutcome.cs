namespace Geeratya.Core.Writes;

/// <summary>What re-reading the issue after a lost reply found.</summary>
public enum VerifyOutcome
{
    /// <summary>The issue shows the intended change.</summary>
    Applied,

    /// <summary>The issue does not show the change.</summary>
    NotApplied,

    /// <summary>The issue could not be read, so nothing is known.</summary>
    Unreachable,
}
