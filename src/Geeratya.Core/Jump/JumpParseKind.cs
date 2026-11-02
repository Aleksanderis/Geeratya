namespace Geeratya.Core.Jump;

/// <summary>What typed jump text means.</summary>
public enum JumpParseKind
{
    /// <summary>Nothing typed.</summary>
    Empty,

    /// <summary>A complete issue key (typed in any case, or a bare number completed with the default project).</summary>
    Key,

    /// <summary>A bare number while no project is known to complete it.</summary>
    NeedsProject,

    /// <summary>Not an issue key.</summary>
    Invalid,
}
