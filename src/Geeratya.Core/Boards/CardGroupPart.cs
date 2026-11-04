namespace Geeratya.Core.Boards;

/// <summary>Where a card sits in the shared frame of a parent and its children in one column.</summary>
public enum CardGroupPart
{
    /// <summary>A card of its own, with its own box.</summary>
    None,

    /// <summary>The parent: it opens the shared frame.</summary>
    First,

    /// <summary>A child with more children after it in the frame.</summary>
    Middle,

    /// <summary>The last child: it closes the shared frame.</summary>
    Last,
}
