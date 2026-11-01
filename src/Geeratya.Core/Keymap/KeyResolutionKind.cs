namespace Geeratya.Core.Keymap;

public enum KeyResolutionKind
{
    /// <summary>No binding starts with the keys typed so far.</summary>
    None,

    /// <summary>The keys typed so far are the start of a longer binding.</summary>
    Prefix,

    /// <summary>The keys typed so far complete a binding.</summary>
    Action,
}
