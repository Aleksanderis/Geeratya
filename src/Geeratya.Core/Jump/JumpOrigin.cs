namespace Geeratya.Core.Jump;

/// <summary>Where a jump row comes from.</summary>
public enum JumpOrigin
{
    /// <summary>The key as typed (completed and upper-cased).</summary>
    Typed,

    /// <summary>An issue of the active tab: its opened issues or its rows.</summary>
    Tab,

    /// <summary>An issue opened earlier on this site in this session.</summary>
    Recent,
}
