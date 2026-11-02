namespace Geeratya.Core.Writes;

/// <summary>Where a rank key moves the selected issue.</summary>
public enum RankMove
{
    /// <summary>Up by the count (default one place).</summary>
    Up,

    /// <summary>Down by the count (default one place).</summary>
    Down,

    /// <summary>To the top of the list or column.</summary>
    Top,

    /// <summary>To the bottom of the list or column.</summary>
    Bottom,
}
