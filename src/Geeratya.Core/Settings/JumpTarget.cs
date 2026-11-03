namespace Geeratya.Core.Settings;

/// <summary>Where the jump prompt opens an issue (<c>:set jump.target</c>).</summary>
public enum JumpTarget
{
    /// <summary>On top of the issue stack of the active tab.</summary>
    Current,

    /// <summary>In a new tab (<c>key = KEY</c>) with the issue on top.</summary>
    Tab,
}
